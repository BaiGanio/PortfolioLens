# Phase 5 — AI layer (your edge)

**Goal:** (1) each app gets an LLM-written modernization summary that is grounded in the facts. (2) An MCP server lets an agent ask questions about the portfolio.

**Covers prep section:** 2F.
**Time:** 4–5 evenings.

## Part A — LLM summary

### Steps
1. Create project `Ai`. Use `Microsoft.Extensions.AI` and its `IChatClient`. Your code talks only to this interface. Foundry and Ollama are two registrations.
2. Deploy one chat model in Azure AI Foundry. Start with a small, cheap one. Auth with Managed Identity if you can; if not, keep the key in Key Vault.
3. Build the **facts document** for one app: frameworks, project count, top packages with versions, findings grouped by rule with counts, score. JSON, compact. **No source code. No file content. No connection strings.**
4. Put a size limit on the facts document. An app with 3,000 findings sends counts and the top 20, not all 3,000.
5. Write the prompt. System part: role, rules ("use only the facts given; if a fact is absent, say unknown"), output format. User part: the facts JSON.
6. Ask for **structured output** (JSON schema): `summary`, `readinessLevel`, `blockers[]` (each one must name a `ruleId` from the input), `steps[]` (ordered), `effort` (S/M/L), `unknowns[]`.
7. **Check the answer in code.** Each `ruleId` in the answer must exist in the input. If not, reject and retry one time; then mark as failed. This is your "grounding" proof.
8. **Cache.** Key = hash of (facts JSON + prompt version + model name). Table `AppSummary`. Same input → no call.
9. **Cost log.** Store input tokens, output tokens, model, duration, and the calculated cost per call. Add `GET /ai/usage`.
10. Budget guard: max tokens per call, max calls per run. Stop and log when the limit is reached.
11. Resilience: timeout, retry on 429 with the `Retry-After` value, circuit breaker.
12. Endpoints: `POST /apps/{id}/summary` (generate, or return the cached one), `GET /apps/{id}/summary`.
13. Mark each summary as "AI-generated, not reviewed". Add a `ReviewedBy` field. Human review is part of the design.
14. **Ollama swap.** Second `IChatClient` registration, chosen by config. Run the same 3 apps on a local model. Compare the quality. Write what you see.
15. **Small eval.** Pick 5 apps. Write down by hand what a good summary must say (for example: "must name WCF as a blocker"). A test runs the 5 and checks those points. Run it when you change the prompt. This is rare among candidates.

### Done when (Part A)
- [ ] Summary for `eShopModernizing` names WCF and WebForms, and each blocker points to a real rule id.
- [ ] A second call for the same app costs 0 tokens (cache hit, visible in the log).
- [ ] `GET /ai/usage` shows the total cost. You know the cost for the full portfolio.
- [ ] A facts document with a fake rule id in the *answer* is rejected (unit test with a fake `IChatClient`).
- [ ] The switch to Ollama is one config value.
- [ ] The 5-app eval runs with one command.

## Part B — MCP server

### Steps
16. Create project `Mcp`. Use the official C# SDK (package `ModelContextProtocol`; check the current version and README before you start).
17. Start with **stdio** transport. Claude Code starts the process itself.
18. Tools (read-only, all of them):
    - `list_apps(framework?, minScore?, maxScore?)`
    - `get_app(name)`
    - `get_findings(app?, ruleId?, severity?, limit)`
    - `find_usages(api)` — "which apps use WCF / `BinaryFormatter` / package X?"
    - `get_package_usage(packageId)` — apps + versions
    - `portfolio_summary()`
    - `compare_apps(a, b)`
19. Write good tool descriptions. The model picks tools by reading them. Say what the tool returns and when to use it.
20. Return small, structured results. Put a hard `limit` on each list (default 50). Say "N more not shown" in the result.
21. The MCP server calls the same query layer as the API. Do not copy queries.
22. Use a **read-only SQL user** for the MCP server.
23. **No "run any SQL" tool.** If you want one for the demo, name the risk in the ADR.
24. Register it in Claude Code: `claude mcp add portfoliolens -- dotnet run --project src/PortfolioLens.Mcp`.
25. Try the demo questions (below). Fix the tool descriptions until the agent picks the right tool the first time.
26. Optional: Streamable HTTP transport, hosted next to the API, with auth. This is the "team shares one server" story.
27. Optional: one MCP **prompt** ("modernization-plan for {app}") and one **resource** (the report of an app).

### Demo script (record it)
1. "Which apps still use WCF on .NET Framework?"
2. "Which 3 apps should we modernize first, and why?"
3. "Which apps does a Newtonsoft.Json major upgrade touch?"
4. "Compare nopCommerce 3.90 and 4.90."
5. "Write a migration plan for the worst app. Use only the findings."

### Done when (Part B)
- [ ] Claude Code lists your tools and answers all 5 demo questions with real data.
- [ ] The read-only user cannot write (test: try an insert, see it fail).
- [ ] A list tool never returns more than its limit.
- [ ] A 2-minute screen recording exists.

## Traps

- **Prompt injection.** Scanned repos are untrusted input. A package name, a file path, or a README can contain "ignore your instructions". You send facts, not code — this cuts most of the risk. But file paths and names still go in. Treat the LLM output as untrusted too: it never triggers an action.
- **The model invents things.** This is why step 7 exists. Do not skip it.
- **Tokens are money.** OrchardCore has 200+ projects. Check the size of the facts document before you send it.
- **Do not send secrets.** Rule `PL3007` must never put a connection string value into a finding message.
- **stdio transport:** anything you write to stdout breaks the protocol. Send logs to stderr.
- **Model output changes** from run to run. Tests on exact text will fail. Test the structure and the grounding.
- **Foundry quota (429)** is low on new subscriptions. Limit the concurrency to 1–2.
- **Data residency.** An on-prem customer can forbid any cloud call. The Ollama swap is your answer.
- Facts from private repos go to Foundry in your own tenant. You said this is fine. Keep them out of the demo video and the public repo.

## Say it in the interview

"The LLM never sees source code. It gets the extracted facts, and it must answer in a schema where each blocker names a rule id. Code checks those ids against the input, so an invented blocker is rejected. Results are cached by input hash, and each call logs tokens and cost."

"I also put an MCP server over the same data. An agent can ask 'which apps still use WCF on .NET Framework' and it gets a real answer from the database, not a guess."

---

## Hints (look only when stuck)

**Chat client**
```csharp
public interface IAppSummarizer
{
    Task<AppSummary> SummarizeAsync(AppFactsForLlm facts, CancellationToken ct);
}
// IChatClient client;   // registered from Azure.AI.OpenAI / Azure.AI.Inference, or OllamaSharp
// var response = await client.GetResponseAsync<AppSummaryDto>(messages, options, cancellationToken: ct);
// response.Usage?.InputTokenCount / OutputTokenCount
```

**Cache key**
```csharp
var key = Convert.ToHexString(SHA256.HashData(
    Encoding.UTF8.GetBytes($"{promptVersion}|{modelId}|{factsJson}")));
```

**Grounding check**
```csharp
var known = facts.Findings.Select(f => f.RuleId).ToHashSet();
var invented = dto.Blockers.Where(b => !known.Contains(b.RuleId)).ToList();
if (invented.Count > 0) { /* retry once with the error in the prompt, then fail */ }
```

**MCP server**
```csharp
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);   // stderr!
builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

[McpServerToolType]
public sealed class PortfolioTools(IPortfolioQueries queries)
{
    [McpServerTool(Name = "find_usages"), Description("Find apps that use an API, namespace, or package. ...")]
    public Task<UsageResult> FindUsages(
        [Description("For example: WCF, BinaryFormatter, System.Web, Newtonsoft.Json")] string api,
        CancellationToken ct) => queries.FindUsagesAsync(api, ct);
}
```

**Test the server without an agent:** `npx @modelcontextprotocol/inspector dotnet run --project src/PortfolioLens.Mcp`
