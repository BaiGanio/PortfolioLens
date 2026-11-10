# Phase 3 — Roslyn analysis + rules

**Goal:** the scanner reads C# code and config files, and creates `Finding` rows that say what blocks modernization. Each app gets a score.

**Covers prep sections:** 2C (parse source code), 2E (third-party API, resilience).
**Time:** 4–5 evenings.

## Steps

### 3A — Rule engine (in `Core` + `Analysis`)
1. Define `Finding`: rule id, severity (`Info/Low/Medium/High`), confidence (`Low/High`), message, file, line, fingerprint.
2. Define the rule interfaces. There are three kinds of input, so three small interfaces are cleaner than one big one:
   - project rule: input = `ProjectFacts`
   - source rule: input = one syntax tree
   - file rule: input = one non-code file (`web.config`, `*.svc`…)
3. Register the rules in DI. The engine gets `IEnumerable<IRule>`. A new rule = one new class + one line.
4. Rule ids: `PL1xxx` project, `PL2xxx` source, `PL3xxx` config, `PL4xxx` package/network.
5. A rule that throws must not stop the scan. Catch, add a `ScanError`, continue.

### 3B — Project rules (no Roslyn — the facts are there from Phase 1)
6. `PL1001` target framework is out of support.
7. `PL1002` target framework support ends in under 12 months.
8. `PL1003` legacy (non-SDK) project format.
9. `PL1004` uses `packages.config`.
10. `PL1005` references `System.Web` / `System.ServiceModel` / `System.EnterpriseServices` assembly.
11. Support dates come from data: Microsoft's `releases-index.json` (`https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json`). Put a copy in the repo. Refresh with a CLI command. .NET Framework dates are not in that file — keep them in your own small JSON.

### 3C — Source rules (Roslyn, syntax only)
12. Add `Microsoft.CodeAnalysis.CSharp`. Parse each `.cs` file to a syntax tree. No compilation.
13. Add `.cs` files to the Phase 1 pipeline. Skip generated files (`*.Designer.cs`, `*.g.cs`, `obj/`).
14. Rules:
    - `PL2001` `using System.Web…` (not `System.Web.Http` alone — think about it)
    - `PL2002` `using System.ServiceModel` / `[ServiceContract]` (WCF)
    - `PL2003` `BinaryFormatter`
    - `PL2004` `WebClient` / `HttpWebRequest`
    - `PL2005` `Thread.Sleep` inside an `async` method
    - `PL2006` `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` (sync over async)
    - `PL2007` `AppDomain.CreateDomain`, Remoting (`MarshalByRefObject`)
    - `PL2008` `async void` that is not an event handler
15. One walk per file for all rules. Do not walk the tree once per rule.
16. Each finding has a line number (for people) and a fingerprint without the line number (for the DB).

### 3D — File rules (cheap, high value)
17. `PL3001` `web.config` has `<system.web>` (classic ASP.NET).
18. `PL3002` config has `<system.serviceModel>` (WCF endpoints). Count them.
19. `PL3003` `*.aspx` / `*.ascx` / `*.master` exist (WebForms). Count them. WebForms = rewrite, not port.
20. `PL3004` `*.svc` or `*.asmx` exist.
21. `PL3005` `*.edmx` exists (EF6 designer model).
22. `PL3006` `Global.asax` exists.
23. `PL3007` connection string in a config file contains a password (report the file, **never store the value**).

### 3E — Package rules (network)
24. `PL4001` a newer **major** version of the package exists.
25. `PL4002` the package is deprecated or has a known vulnerability (optional).
26. Client for the NuGet V3 API. Typed `HttpClient` + `AddStandardResilienceHandler()` from `Microsoft.Extensions.Http.Resilience`.
27. Ask one time per package id, not one time per project. Cache the answers in the DB with a fetch time (TTL 24 h).
28. Limit concurrency (for example 8 requests at one time).
29. Offline switch `--offline`: skip network rules, and write an `Info` finding that says so. This is the on-prem story.
30. Tests: record real NuGet responses to files. Replay them from a fake `HttpMessageHandler`. Tests never call the network.

### 3F — Score + report
31. Readiness score per app, 0–100. Simple: start at 100, subtract weight × count per rule, with a cap per rule. Keep the weights in config.
32. Add `GET /apps/{id}/report` → Markdown or HTML: frameworks, top findings, score, "what to do first".
33. Add `GET /portfolio/summary` → apps per framework, worst 10 apps, most-used old packages.

### 3G — Optional: git facts
34. Last commit date, commits in the last 12 months, number of authors. Run `git log` as a process, or use `LibGit2Sharp`.
35. `PL5001` no commit in 24 months + out-of-support framework = high risk.

## Done when

- [ ] One fixture per rule, with one positive case and one negative case. All tests green.
- [ ] `eShopModernizing` gets WCF, WebForms, `System.Web`, legacy-project findings.
- [ ] `dotnet/eShop` gets almost no findings and a high score.
- [ ] nopCommerce 3.90 score is much lower than nopCommerce 4.90 score.
- [ ] Unplug the network: the scan completes with `--offline`, and also without it (slower, with errors logged, no crash).
- [ ] A second scan makes no duplicate findings. A fixed finding gets marked as gone.
- [ ] You wrote down 3 real false positives that you found, and what you did.
- [ ] Scan time for OrchardCore: you know how much Roslyn added.

## Traps

- **Syntax only = no types.** `WebClient` can be someone's own class. `.Result` can be a property of a DTO. This is why `confidence` exists. Say this limit before the interviewer does.
- The semantic model needs a compilation: all references, restore, often a build. Old projects do not build on your machine. This is the real reason for syntax-first.
- `Thread.Sleep` in async: you must know the enclosing method. Also check lambdas and local functions.
- Comments and strings: Roslyn handles them right. A regex does not. This is your answer to "why not grep?".
- Code inside `#if NETFRAMEWORK`: the parser skips inactive regions. Decide: parse with several preprocessor symbol sets, or accept the miss. Write an ADR.
- Syntax trees are big. Do not keep them. Parse → walk → findings → release.
- NuGet API: package ids are lower-case in the URL. Private packages return 404 — this is normal, not an error.
- Pre-release versions: ignore them for "newer major exists".
- `System.Web.Http` (Web API 2) and `System.Web.Mvc` are different migration costs than `System.Web.UI` (WebForms). Different rules or different severity.

## Say it in the interview

"Rules are small classes behind an interface, registered in DI. Roslyn runs in syntax-only mode, so the scanner needs no build and works on code that does not compile. The price is no type information, so each finding has a confidence level."

---

## Hints (look only when stuck)

**Interfaces**
```csharp
public interface IRule { string Id { get; } Severity DefaultSeverity { get; } }
public interface IProjectRule : IRule { IEnumerable<Finding> Check(ProjectFacts project, RuleContext ctx); }
public interface ISourceRule  : IRule { void Register(SourceRuleRegistry registry); }   // subscribes to node kinds
public interface IFileRule    : IRule { bool Matches(string fileName); IEnumerable<Finding> Check(FileInput file); }
```

**One walk, many rules**
```csharp
// registry: Dictionary<SyntaxKind, List<Action<SyntaxNode, FindingSink>>>
sealed class DispatchWalker(SourceRuleRegistry reg, FindingSink sink) : CSharpSyntaxWalker
{
    public override void Visit(SyntaxNode? node)
    {
        if (node is not null && reg.TryGet(node.Kind(), out var handlers))
            foreach (var h in handlers) h(node, sink);
        base.Visit(node);
    }
}
```

**Parse**
```csharp
var text = SourceText.From(stream);                       // stream, not string
var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest), path, ct);
var root = await tree.GetRootAsync(ct);
var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
```

**Thread.Sleep in async**
```csharp
// node: InvocationExpressionSyntax with Expression = MemberAccess "Thread.Sleep"
// walk up: node.Ancestors() → first MethodDeclarationSyntax / LocalFunctionStatementSyntax /
//          AnonymousFunctionExpressionSyntax → has Modifiers.Any(SyntaxKind.AsyncKeyword) ?
```

**NuGet**
```
GET https://api.nuget.org/v3-flatcontainer/{id-lowercase}/index.json   → { "versions": [ ... ] }
Service index: https://api.nuget.org/v3/index.json  (find resources by @type; do not hard-code the other URLs)
```
```csharp
services.AddHttpClient<INuGetClient, NuGetClient>(c => c.BaseAddress = new("https://api.nuget.org/"))
        .AddStandardResilienceHandler();   // retry + circuit breaker + timeouts + rate limiter
// compare with NuGet.Versioning: NuGetVersion.Parse(v).Major
```

**Fingerprint:** hash of `ruleId + relativeFile + enclosing member name + normalized node text`.
