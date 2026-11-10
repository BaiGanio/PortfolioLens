# Phase 4 — Azure pipeline

**Goal:** upload a zip of a repo. Some seconds later, the findings are in Azure SQL and the API shows them. No secret is in any config file.

**Covers prep sections:** 2B (Azure), 2H (system design). These are your stated gaps, so go slow here.
**Time:** 6–8 evenings. Part A is local. Part B is real Azure.

```
client → POST /uploads → Blob (uploads/) → Event Grid → Service Bus queue (scan-jobs)
       → Function (isolated) → extract zip → Phase 1 + 3 → Azure SQL → API (Container Apps)
                            ↘ failure × N → dead-letter queue
```

## Part A — Local (no cost)

### Steps
1. Hide the infrastructure behind interfaces in `Core`: `IBlobStore`, `IJobQueue`. You need these again in Phase 6.
2. Add to `docker-compose.yml`: **Azurite** (Blob emulator) and the **Service Bus emulator** (`mcr.microsoft.com/azure-messaging/servicebus-emulator`; it needs a SQL Server container, and you have one).
3. API: `POST /uploads` takes a zip as a stream, writes it to Blob, creates a `ScanRun` with status `Queued`, returns `202 Accepted` + the run id + a `Location` header to `GET /scan-runs/{id}`.
4. There is no Event Grid emulator. Locally, the API sends the queue message itself after the upload. Put this behind a config switch. In Azure, Event Grid does it.
5. Message body: run id, blob name, app name. Small. Never put the zip in the message.
6. Create the `Worker` project: Azure Functions, **isolated worker**, .NET 10. One function with a Service Bus trigger.
7. The function: download the blob to temp → safe unzip → run the Phase 1 + 3 pipeline → save with `IScanStore` → set the run to `Completed` → delete the temp folder (in `finally`).
8. **Idempotent handler.** Service Bus is at-least-once. The same message can come two times. Check the `ScanRun` status first. Phase 2 upserts save you here.
9. **Safe unzip:** limit the total unpacked size, the entry count, and the compression ratio (zip bomb). Check that each entry's full path stays inside the target folder (zip slip).
10. **Poison test:** upload a broken zip. See the retries. See the message in the dead-letter queue. See the `ScanRun` as `Failed` with a clear error.
11. Separate the two error kinds. A broken zip never gets better: dead-letter it at once with a reason. A SQL timeout is transient: let it retry.
12. Add a small `GET /admin/dead-letters` endpoint or a CLI command that lists the DLQ messages.
13. OpenTelemetry in the API and in the worker. Locally, export to the console or to the Aspire dashboard container. One trace must show: upload → queue → function → SQL.

### Done when (Part A)
- [ ] `docker compose up` + `func start` + API run. `curl -F file=@repo.zip localhost:.../uploads` ends with findings in SQL.
- [ ] The same zip two times → no duplicates.
- [ ] Broken zip → DLQ + `Failed` run. A zip bomb is refused.
- [ ] Kill the function in the middle → the message comes back → the run completes.
- [ ] One trace id covers the API and the function.

## Part B — Real Azure (a few euro)

### Before you start
- Set a **budget alert** (€10) on the subscription.
- One resource group: `rg-portfoliolens-dev`. One region.
- Naming: `plens` + short random suffix (storage account names are global).

### Steps
14. Write `infra/main.bicep` with modules. Resources:
    - Storage account + container `uploads`
    - Service Bus namespace (**Basic** tier is enough for one queue) + queue `scan-jobs` (max delivery count 5, lock duration 5 min)
    - Event Grid system topic on the storage account + subscription: `BlobCreated` → Service Bus queue
    - Function App on **Flex Consumption** (see trap about timeouts)
    - Azure SQL: serverless, free offer
    - Container Apps environment + the API container app (scale 0–1)
    - Container Registry (Basic), or use GitHub Container Registry and save the cost
    - Key Vault
    - Log Analytics workspace + Application Insights
    - User-assigned managed identity + role assignments
15. Run `az deployment group what-if` before each deploy. Read it.
16. **Managed Identity everywhere.** Role assignments in Bicep:
    - worker → `Storage Blob Data Reader`, `Azure Service Bus Data Receiver`
    - API → `Storage Blob Data Contributor`
    - Event Grid system topic identity → `Azure Service Bus Data Sender`
    - both → SQL user created with `CREATE USER [name] FROM EXTERNAL PROVIDER`
17. Code: `DefaultAzureCredential`. Config has only names and URLs (`ServiceBusConnection__fullyQualifiedNamespace`), no keys.
18. Switch off key access where you can: storage `allowSharedKeyAccess: false`, Service Bus `disableLocalAuth: true`, SQL Entra-only authentication. Then nobody can use a leaked key, because there is no key.
19. Key Vault: with Managed Identity you have almost no secrets. Put one real third-party secret there (the Foundry key for Phase 5, or a GitHub token for NuGet rate limits). Read it with a Key Vault reference.
20. Event Grid message: the body is now the Event Grid event, not your own JSON. The function must read the blob URL from it. Handle both shapes, or make local send the same shape.
21. Application Insights: `Azure.Monitor.OpenTelemetry` in both apps. Open the "End-to-end transaction" view. Take a screenshot for the README.
22. Add custom metrics: files scanned, findings per run, scan duration.
23. GitHub Actions `deploy.yml`: login with **OIDC federated credentials** (no client secret in GitHub) → Bicep deploy → build + push the API image → publish the function → EF migration.
24. Migrations: generate an idempotent script (`dotnet ef migrations script --idempotent`) or a migration bundle. Run it as a pipeline step. Do not migrate at app startup.
25. Load test: upload all your scan targets in a loop (10–20 zips). Watch the queue length, the function instances, the SQL DTU/vCore.
26. Kusto: write 3 queries in Log Analytics (failed runs, slowest scans, DLQ count). Save them in `docs/`.
27. **Delete the resource group.** Then deploy again from zero with one command. This proves the Bicep is complete.
28. Delete again when you are done. Purge the Key Vault (soft delete keeps the name for days).

### Done when (Part B)
- [ ] `az deployment group create` from an empty resource group gives a working system.
- [ ] `az storage blob upload` (with your user identity) → findings show in the API.
- [ ] A search of the repo and of the app settings finds no key, no password, no connection string with a secret.
- [ ] Broken zip → DLQ in Azure. You see it in the portal and in a Kusto query.
- [ ] One screenshot of the end-to-end trace.
- [ ] GitHub Actions deploys with OIDC.
- [ ] The total cost is written in the README. The resource group is deleted.

## Traps

- **Function timeout.** Classic Consumption: 5 min default, 10 min max. A big repo takes longer. Flex Consumption allows long runs. Or use a Container Apps job. Know this trade-off; it is a certain interview question.
- **Message lock.** Max lock duration is 5 min. The Functions host renews the lock for you up to `maxAutoLockRenewalDuration`. Set it longer than your longest scan, or the message becomes visible again and a second instance starts the same job.
- **Temp disk.** Functions have small temp storage. A big zip + unpacked files can fill it. Stream where you can; limit the upload size.
- **Event Grid → Service Bus** delivery needs the role on the *system topic's* identity, not on yours.
- **Role assignments take a minute or two** to work. The first run after a deploy can fail with 401/403. Retry before you debug.
- **Azure SQL serverless auto-pause.** The first connection after a pause takes ~1 min and can time out. Use retry (`EnableRetryOnFailure`).
- **Azure SQL firewall.** "Allow Azure services" is the simple way. Private endpoints are the right way but cost money. Know the difference; build the simple one.
- **Service Bus Basic** has no topics, no sessions, no duplicate detection. It has a DLQ. Fine for this.
- **Blob trigger vs Event Grid.** The classic blob trigger polls and can be slow by minutes. Event Grid is push. Say this if they ask "why not a blob trigger?".
- **In-process Functions model is retired** (support ended 10 Nov 2026). Isolated worker is the only choice. Good to say.
- **Cold start** on scale-to-zero. Fine here. Say what you would change for a real product (min instances = 1).

## Say it in the interview

"I had not used Functions and Service Bus in production, so I built the pipeline: Blob, Event Grid, Service Bus, an isolated Function, Azure SQL, all with Managed Identity and with key access switched off. It deploys from an empty resource group with one Bicep command. I tested the dead-letter path with a broken zip on purpose."

---

## Hints (look only when stuck)

**Interfaces**
```csharp
public interface IBlobStore
{
    Task UploadAsync(string name, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string name, CancellationToken ct);
}
public interface IJobQueue { Task EnqueueAsync(ScanJob job, CancellationToken ct); }
```

**Function**
```csharp
[Function("ScanJob")]
public async Task Run(
    [ServiceBusTrigger("scan-jobs", Connection = "ServiceBusConnection")] ServiceBusReceivedMessage msg,
    ServiceBusMessageActions actions, CancellationToken ct)
{
    // parse → if bad input: await actions.DeadLetterMessageAsync(msg, deadLetterReason: "InvalidZip", ...); return;
    // transient failure: throw → host abandons → retry → DLQ after MaxDeliveryCount
}
```

**Program.cs (worker)**
```csharp
var builder = FunctionsApplication.CreateBuilder(args);
builder.Services.AddOpenTelemetry().UseAzureMonitor();          // + UseFunctionsWorkerDefaults()
builder.Services.AddAzureClients(c => {
    c.AddBlobServiceClient(new Uri(cfg["Storage:BlobUri"]!));
    c.UseCredential(new DefaultAzureCredential());
});
```

**SQL connection string (no password)**
```
Server=tcp:<name>.database.windows.net;Database=plens;Authentication=Active Directory Default;Encrypt=True;
```

**Zip slip check**
```csharp
var dest = Path.GetFullPath(Path.Combine(root, entry.FullName));
if (!dest.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
    throw new InvalidZipException("entry escapes the target folder");
```

**Bicep role assignment**
```bicep
resource sbReceiver 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: queue
  name: guid(queue.id, identity.id, 'receiver')
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '<role-guid>')
  }
}
```

**Commands**
```bash
az deployment group what-if -g rg-portfoliolens-dev -f infra/main.bicep -p infra/dev.bicepparam
az deployment group create  -g rg-portfoliolens-dev -f infra/main.bicep -p infra/dev.bicepparam
az group delete -n rg-portfoliolens-dev --yes
```
