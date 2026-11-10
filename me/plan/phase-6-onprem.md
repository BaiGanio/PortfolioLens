# Phase 6 — On-prem mode (optional, but it matches the job ad)

**Goal:** the same code runs with no Azure at all, on your k3s homelab. A customer who cannot reach you can send you a diagnostics bundle.

**Covers prep section:** 2E ("enterprise software, deployed in complex customer environments").
**Time:** 4–6 evenings.

## Steps

### 6A — Swap the infrastructure
1. You have `IBlobStore` and `IJobQueue` from Phase 4. Add a second implementation of each:
   - `FileSystemBlobStore` (a folder; on k3s, a persistent volume)
   - `SqlJobQueue` (a table in the same SQL Server)
2. One config value picks the mode: `Hosting:Mode = Azure | OnPrem`. Validate it at startup.
3. Replace the Azure Function with a `BackgroundService` worker for on-prem. The function and the worker are both thin: they call the same `ScanJobHandler`.

### 6B — The SQL queue (the interesting part)
4. Table `JobQueue`: id, payload, status, attempts, visible-after time, locked-by, locked-until, last error, created.
5. **Claim** a job in one statement so two workers never get the same row (see hints: `UPDLOCK, READPAST`).
6. **Visibility timeout:** a claimed job has `LockedUntil`. If the worker dies, the job becomes visible again after that time. The worker renews the lock during a long scan.
7. **Retry with back-off:** on failure, `Attempts + 1`, `VisibleAfter = now + back-off`.
8. **Poison:** after N attempts, status `DeadLettered` with the error. Same behavior as the Service Bus DLQ.
9. Polling: when the queue is empty, wait with a back-off (1 s → 30 s). No busy loop.
10. Test: start 4 workers, enqueue 100 jobs, each job is processed exactly one time. Kill a worker in the middle; its job completes later on a different worker.

### 6C — "You cannot SSH in"
11. **Config validation at startup** (`ValidateDataAnnotations` + `ValidateOnStart`). A wrong setting stops the app with a message that names the setting and the fix.
12. **Health checks:** `/health/live` (the process is up), `/health/ready` (DB reachable, storage writable, queue reachable). Each check has a name and a clear failure text.
13. **Preflight command:** `portfoliolens preflight` checks the SQL version, the permissions, the disk space, the write access, the outbound network (NuGet, LLM). It prints a table of pass/fail. Customers run it before they call support.
14. **Diagnostics bundle:** `GET /admin/diagnostics` (auth required) returns a zip: app version, runtime + OS info, effective config **with secrets removed**, last N log lines, last 20 scan runs with errors, health check results, DB migration state.
15. **Secret redaction:** remove values by key name (`password`, `secret`, `key`, `token`, `connectionstring`) and also parse connection strings to remove the password part. Write a test with a fake secret; the test fails if the secret text is in the bundle.
16. **Structured logs** to files with rotation (Serilog or the built-in JSON console + the platform). Each log line has the scan run id.
17. **Offline mode:** `--offline` from Phase 3 + the copy of the support-dates file + the Ollama client from Phase 5. The app must start and scan with no internet at all. Test: block the network of the container.
18. **Proxy and corporate certificates:** the `HttpClient` honors `HTTPS_PROXY`. Document how to add a corporate root certificate to the container. You do not need to build more than this; know the answer.

### 6D — Upgrades
19. Migrations do not run at startup. There is a `portfoliolens migrate` command (or a migration bundle). It prints what it will do, and it supports `--dry-run` (script only).
20. The app checks the DB schema version at startup. If the DB is older or newer than the app expects, it stops with a clear message.
21. Write the upgrade and rollback steps in `docs/upgrade.md`: backup → migrate → deploy → verify → (rollback = restore + old image). Know why "down" migrations are risky with data.
22. Write a **support matrix** in the README: SQL Server versions you tested (2019, 2022), .NET runtime, OS/container, CPU architecture.

### 6E — Run it on k3s
23. Dockerfiles for the API and the worker. Multi-stage, non-root user, `linux/arm64` + `linux/amd64` images (`docker buildx`).
24. Kubernetes manifests or a small Helm chart: deployments, service, ingress, config map, secret, persistent volume claim, liveness/readiness probes that use your health endpoints.
25. **SQL Server has no ARM64 Linux image.** If the homelab is a Raspberry Pi: run SQL Server on the Mac or an x86 machine and point the cluster at it. (Second option: add PostgreSQL as a second EF Core provider. More work. Decide here.)
26. Scale the worker to 3 replicas. Run 30 scan jobs. Watch the SQL queue work.
27. Do an upgrade for real: deploy v1, scan, add a migration, deploy v2 with the steps from `docs/upgrade.md`.

## Done when

- [ ] `Hosting:Mode=OnPrem` + `docker compose up` runs the full flow with no Azure package making a network call.
- [ ] The 4-workers/100-jobs test is green and stable (run it 20 times).
- [ ] A wrong connection string gives one clear startup error, not a stack trace on the first request.
- [ ] The diagnostics bundle test proves that no secret is inside.
- [ ] The container scans with its network blocked.
- [ ] The system runs on k3s with 3 worker replicas.
- [ ] One real upgrade v1 → v2 is done and written down.

## Traps

- The SQL queue claim without `READPAST` makes workers wait for each other. Without `UPDLOCK`, two workers get the same row.
- Use the DB server's clock (`SYSUTCDATETIME()`) for lock times, not each worker's clock.
- A queue table gets hot and fragmented. Delete or archive completed rows. Index on (status, visible-after).
- Self-contained deployment makes a bigger image but removes "wrong runtime on the customer's machine". Know both sides.
- `ValidateOnStart` is not the default. Without it, a bad option fails at the first use, at 2 a.m.
- Redaction by key name misses secrets inside URLs (`https://user:pass@host`). Handle it.
- Do not log the facts document or the config at `Information` level.

## Say it in the interview

"Blob and queue are behind two interfaces. In Azure they are Blob Storage and Service Bus. On-prem they are a folder and a SQL table with a `READPAST` claim, visibility timeout, and dead-lettering. Same handler code. Because I cannot reach a customer's server, the app validates its config at startup, has a preflight command, and can produce a diagnostics bundle with the secrets removed."

---

## Hints (look only when stuck)

**Claim one job**
```sql
WITH next AS (
    SELECT TOP (1) *
    FROM dbo.JobQueue WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE Status = 'Queued' AND VisibleAfter <= SYSUTCDATETIME()
       OR Status = 'Running' AND LockedUntil < SYSUTCDATETIME()      -- worker died
    ORDER BY Id
)
UPDATE next
SET Status = 'Running', LockedBy = @worker,
    LockedUntil = DATEADD(SECOND, @lockSeconds, SYSUTCDATETIME()),
    Attempts = Attempts + 1
OUTPUT inserted.Id, inserted.Payload, inserted.Attempts;
```
(Mind the `AND`/`OR` precedence — add parentheses in your real version.)

**Worker**
```csharp
public sealed class ScanWorker(IJobQueueReader queue, IServiceScopeFactory scopes, ILogger<ScanWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        // loop: claim → if none: delay with back-off → else: create scope → handler.HandleAsync(job, stop)
        //       success: complete; exception: fail(job, error, nextVisibleAfter) or dead-letter
    }
}
```

**Options validation**
```csharp
services.AddOptions<HostingOptions>()
        .BindConfiguration("Hosting")
        .ValidateDataAnnotations()
        .Validate(o => o.Mode != HostingMode.OnPrem || Directory.Exists(o.BlobRoot), "Hosting:BlobRoot does not exist")
        .ValidateOnStart();
```

**Health checks**
```csharp
services.AddHealthChecks()
        .AddDbContextCheck<PortfolioDb>("database", tags: ["ready"])
        .AddCheck<BlobStoreHealthCheck>("blob-store", tags: ["ready"]);
app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") });
app.MapHealthChecks("/health/live",  new() { Predicate = _ => false });
```

**Multi-arch image:** `docker buildx build --platform linux/amd64,linux/arm64 -t <registry>/plens-api:1.0.0 --push .`
