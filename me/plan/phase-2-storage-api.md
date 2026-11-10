# Phase 2 — Storage + API

**Goal:** scan results live in SQL Server. A second scan makes no duplicates. An API reads the data.

**Covers prep sections:** 2A (EF Core, DI), 2D (SQL Server), 2G (testing).
**Time:** 4–5 evenings.

## Steps

### 2A — Database in Docker
1. Add `infra/docker-compose.yml` with SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-latest`). On Apple Silicon, set `platform: linux/amd64`. It runs with emulation, and this is fine.
2. Keep the SA password in `.env` (already in `.gitignore`).

### 2B — EF Core model (project `Data`)
3. Entities: `App`, `Project`, `Package`, `ProjectPackage` (join, holds the version), `ProjectReference`, `Finding`, `ScanRun`, `ScanFile` (path + content hash).
4. Give each entity a **natural key** with a unique index:
   - `App`: `Name`
   - `Project`: `AppId + RelativePath`
   - `Package`: `PackageId` (the NuGet id)
   - `ProjectPackage`: `ProjectId + PackageId`
   - `Finding`: `ProjectId + RuleId + Fingerprint` (see trap below)
   - `ScanFile`: `AppId + RelativePath`
5. Set string lengths. `nvarchar(max)` cannot be in an index key.
6. Create the first migration. Read the generated SQL before you apply it.

### 2C — Idempotent write
7. Write `IScanStore.SaveAsync(AppFacts, scanRunId, ct)`.
8. First version: load the app graph, compare, insert/update/delete in one transaction. Make the tests green.
9. Content hash: SHA-256 per project file, stored in `ScanFile`. If the hash is the same, skip the parse and the write.
10. Second version (for big apps): bulk path. Write the rows into a staging table with `SqlBulkCopy`, then one `MERGE` (or `UPDATE` + `INSERT`) into the real table. Measure both versions on OrchardCore (200+ projects).
11. Things that disappeared: a project that was deleted from the repo must go away (or get `LastSeenRunId`). Decide, and write an ADR.

### 2D — ScanRun (checkpoint + resume)
12. `ScanRun`: id, started, finished, status (`Running/Completed/Failed/Cancelled`), counts (apps, projects, files skipped, errors), duration.
13. `ScanRunApp`: one row per app in the run, with status. This is the checkpoint.
14. `scan --resume <runId>`: process only the apps that are not `Completed`.
15. Kill the process in the middle (Ctrl+C, and also `kill -9`). Resume. Check the result is the same as a clean run.

### 2E — Wire the CLI
16. `scan <folder> --db` runs Phase 1 and saves to SQL. The JSON output stays as an option.

### 2F — Minimal API (project `Api`)
17. Endpoints:
    - `GET /apps` (paged, filter by framework)
    - `GET /apps/{id}`
    - `GET /apps/{id}/dependencies` (packages + project references)
    - `GET /findings?severity=high&ruleId=...` (paged)
    - `GET /scan-runs`, `GET /scan-runs/{id}`
    - `GET /packages/{packageId}/usages` (which apps use it, which versions)
18. Read queries: `AsNoTracking` + projection to DTOs. Never return entities.
19. Paging: keyset (`WHERE Id > @last ORDER BY Id`) for findings. Offset is fine for apps.
20. Errors as `ProblemDetails`. OpenAPI with the built-in `Microsoft.AspNetCore.OpenApi`.
21. Health checks: `/health/live`, `/health/ready` (ready = DB reachable).

### 2G — Tests
22. Integration tests with `Testcontainers.MsSql`. One container for the whole test collection. Reset the data between tests with `Respawn`.
23. API tests with `WebApplicationFactory<Program>`. Replace the connection string with the container's.
24. Must-have tests:
    - save the same `AppFacts` two times → row counts are equal
    - change one package version → exactly one row changes
    - unchanged hash → the parser is not called
    - two saves of the same app at the same time → no duplicate, no deadlock
    - resume after a failure completes the run

### 2H — Look at the SQL (do this, do not skip)
25. Turn on EF logging. For each endpoint, read the SQL.
26. In SSMS or Azure Data Studio, look at the execution plan of `GET /findings?severity=high`. Find a scan. Add an index. See the seek. Then add `INCLUDE` columns and see the key lookup go away.
27. Write the before/after numbers in an ADR. This is your second "data performance" story.

## Done when

- [ ] `docker compose up` + `dotnet ef database update` gives a working DB.
- [ ] Scan `scan-input/` two times. Row counts are identical. The second run is much faster (hash skip). You know both times.
- [ ] All tests in 2G are green in CI (GitHub runners have Docker).
- [ ] `kill -9` + `--resume` gives the same data as a clean run.
- [ ] No endpoint has an N+1 query. You checked the log.
- [ ] You can show one execution plan before and after an index.
- [ ] Bulk path vs simple path: you have both numbers.

## Traps

- **EF Core has no upsert.** You build it: read-then-write, or raw `MERGE`, or a bulk library.
- `MERGE` has race conditions without `WITH (HOLDLOCK)`.
- The unique index is your safety net. Catch SQL errors 2601 and 2627, and retry.
- **Finding fingerprint:** do not use the line number. A new line at the top of the file moves all findings, and each one looks "new". Use rule + file + the symbol or the code text hash.
- Captive dependency: do not put `DbContext` in a singleton. In the CLI, make a scope per app.
- A `DbContext` that tracks 100,000 entities is slow. Make a new context per app, or use the bulk path.
- `Include` on two collections = cartesian explosion. Use projection or `AsSplitQuery`.
- One huge transaction → lock escalation to a table lock. One transaction per app is the right size.
- The first Testcontainers start downloads ~1.5 GB. Do it before you sit down to code.
- `WebApplicationFactory` needs `public partial class Program;` at the end of `Program.cs`.

## Say it in the interview

"A re-scan is idempotent. Each entity has a natural key with a unique index, and a content hash skips unchanged files. I tested it with a real SQL Server in Testcontainers, including a kill in the middle of a run and a resume."

---

## Hints (look only when stuck)

**Store**
```csharp
public interface IScanStore
{
    Task<Guid> StartRunAsync(CancellationToken ct);
    Task SaveAppAsync(Guid runId, AppFacts facts, CancellationToken ct);
    Task CompleteRunAsync(Guid runId, ScanRunStatus status, CancellationToken ct);
    Task<IReadOnlyDictionary<string, string>> GetFileHashesAsync(string appName, CancellationToken ct);
}
```

**Model config**
```csharp
b.Entity<Project>().HasIndex(p => new { p.AppId, p.RelativePath }).IsUnique();
b.Entity<Finding>().HasIndex(f => new { f.Severity, f.RuleId })
                   .IncludeProperties(f => new { f.ProjectId, f.Message });
```

**Read query**
```csharp
app.MapGet("/apps", async (PortfolioDb db, int page = 1, int size = 50, CancellationToken ct = default) =>
    TypedResults.Ok(await db.Apps.AsNoTracking()
        .OrderBy(a => a.Name)
        .Skip((page - 1) * size).Take(size)
        .Select(a => new AppDto(a.Id, a.Name, a.Projects.Count))
        .ToListAsync(ct)));
```

**Bulk path**
```csharp
// 1. CREATE TABLE #stage (...)          — same connection, same transaction
// 2. new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, tx) { DestinationTableName = "#stage" }
// 3. MERGE dbo.ProjectPackage WITH (HOLDLOCK) AS t USING #stage AS s ON ... 
//    WHEN MATCHED AND t.Version <> s.Version THEN UPDATE ...
//    WHEN NOT MATCHED BY TARGET THEN INSERT ...
//    WHEN NOT MATCHED BY SOURCE AND t.ProjectId IN (...) THEN DELETE;
```

**Hash:** `Convert.ToHexString(await SHA256.HashDataAsync(stream, ct))`

**Tests**
```csharp
public sealed class SqlFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder().Build();
    public string ConnectionString => _sql.GetConnectionString();
    // InitializeAsync: start + migrate. DisposeAsync: dispose.
}
[CollectionDefinition("sql")] public class SqlCollection : ICollectionFixture<SqlFixture>;
```

**Useful SQL**
```sql
SET STATISTICS IO, TIME ON;
SELECT * FROM sys.dm_db_index_usage_stats WHERE database_id = DB_ID();
```
