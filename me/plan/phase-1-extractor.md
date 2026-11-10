# Phase 1 — Extractor (console app)

**Goal:** point the CLI at a folder of repos. Get one JSON file per app with its projects, frameworks, packages, and references.

**Covers prep sections:** 2A (async, big data), 2C (extraction, reliability).
**Time:** 3–4 evenings.

## The data model (in `Core`)

Build these as records. Keep them plain. No EF attributes here.

- `AppFacts` — name, root path, solutions, projects, scan errors, counts.
- `ProjectFacts` — relative path, language (C#/VB/F#), style (Sdk / Legacy), target frameworks (list — a project can have many), output type, package references, project references, assembly references.
- `PackageRef` — id, version (can be null), source (`PackageReference` / `PackagesConfig` / `CentralPackage`).
- `ScanError` — file path, error kind, message.

## Steps

### 1A — Walk the tree
1. Write a file walker that returns project files lazily (`.csproj`, `.vbproj`, `.fsproj`, `.sln`, `.slnx`, `packages.config`, `Directory.Packages.props`, `Directory.Build.props`, `global.json`).
2. Do not go into `bin`, `obj`, `.git`, `node_modules`, `packages`, `.vs`.
3. Do not follow symlinks. Ignore folders that you cannot open.
4. A sub-folder of the input folder = one app.

### 1B — Read one project file
5. Detect the style. SDK-style has an `Sdk` attribute (or an `<Sdk>` element, or `<Import Sdk=...>`). Legacy has the xmlns `http://schemas.microsoft.com/developer/msbuild/2003` and `ToolsVersion`.
6. Read the target framework. Three shapes: `<TargetFramework>`, `<TargetFrameworks>` (list with `;`), `<TargetFrameworkVersion>` (legacy, `v4.7.2`).
7. Normalize all three into one form. Use the `NuGet.Frameworks` package.
8. Read `PackageReference` items. The version can be an attribute or a child element. It can be absent (central packages).
9. Read `packages.config` next to a legacy project.
10. Read `ProjectReference` items. Normalize the path (Windows `\` on your Mac).
11. Read `<Reference>` items of legacy projects (`System.Web`, `System.ServiceModel` are gold for Phase 3).

### 1C — Resolve what one file cannot tell you
12. Central Package Management: find the nearest `Directory.Packages.props` above the project. Fill the missing versions from it.
13. `Directory.Build.props`: if the project has no target framework, look for it in the nearest props file above.
14. Properties such as `$(NetVersion)`: resolve the simple case (property defined in the same file or in the props file). If you cannot resolve it, keep the raw text and add a `ScanError` of kind `Unresolved`. Do not guess.

### 1D — Make it a pipeline
15. Stage 1 (producer): the walker writes file paths into a **bounded** `Channel<T>`.
16. Stage 2 (workers): N readers parse the files in parallel. Use `Parallel.ForEachAsync` over `reader.ReadAllAsync()`.
17. Stage 3 (writer): one consumer groups the results per app and writes JSON with `System.Text.Json` (source generator, stream to file).
18. One `CancellationToken` goes through all stages. Ctrl+C stops clean, and the partial output is valid JSON.
19. An exception in one file becomes a `ScanError`. The run continues.

### 1E — CLI
20. Arguments: `scan <inputFolder> --out <folder> --parallel <n>`. Use `System.CommandLine` or plain `args`.
21. At the end, print: apps, projects, errors, time, peak memory.

## Done when

- [ ] Unit tests on the fixtures: SDK-style, legacy + `packages.config`, multi-target, central packages, unresolved property, broken XML.
- [ ] Scan all of `scan-input/`. No crash. Output is one JSON per app.
- [ ] `dotnet/eShop` output has package versions (they come from `Directory.Packages.props`).
- [ ] `eShopModernizing` output shows legacy projects with `net472`-like frameworks and `packages.config` packages.
- [ ] Scan `dotnet/roslyn` (very large). Memory stays flat. Write the peak number down.
- [ ] Ctrl+C in the middle → the process stops in under 2 seconds, with no stack trace.
- [ ] Run it two times → the output is byte-identical (sorted, deterministic). You need this in Phase 2.
- [ ] You measured 3 values of `--parallel` and you know which one is fastest and why.

## Traps

- `Directory.GetFiles` loads all paths in memory. Use the lazy API.
- Recursive `EnumerateFiles` cannot skip a folder. It goes into `node_modules` anyway. You need your own recursion or `FileSystemEnumerable<T>`.
- Legacy XML has a namespace. `Element("TargetFrameworkVersion")` returns null without it.
- Items can have `Condition` attributes. Read them all, and store the condition text. Do not evaluate it.
- `PackageReference` uses `Include`, and sometimes `Update`. Ignore `Update` in project files.
- An unbounded channel has no back-pressure. The walker is much faster than the parser, so memory grows.
- Forgetting `writer.Complete()` → the readers wait forever.
- Parallel output is in random order. Sort before you write.
- File I/O in parallel: more threads is not faster on one SSD after a point. Measure.

## Extra fixtures to add

`multi-target/`, `central-packages/` (with `Directory.Packages.props`), `props-inherit/`, `broken-xml/`, `vbproj-legacy/`, `unresolved-property/`.

## Say it in the interview

"The extractor is a three-stage pipeline with a bounded channel. The walker is faster than the parser, so the bound gives back-pressure and memory stays flat. I measured it on dotnet/roslyn."

---

## Hints (look only when stuck)

**Walker**
```csharp
IEnumerable<string> FindProjectFiles(string root);          // lazy
// FileSystemEnumerable<string> with:
//   ShouldRecursePredicate = (ref FileSystemEntry e) => !Skip.Contains(e.FileName.ToString())
//   ShouldIncludePredicate = (ref FileSystemEntry e) => !e.IsDirectory && IsInteresting(e.FileName)
// EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }
```

**Reader**
```csharp
public interface IProjectFileReader
{
    ValueTask<ProjectFacts> ReadAsync(string path, CancellationToken ct);
}
// XDocument.LoadAsync(stream, LoadOptions.None, ct)
// XNamespace ns = doc.Root!.Name.Namespace;   // empty for SDK-style, 2003 for legacy
// doc.Descendants(ns + "PackageReference")
// NuGetFramework.Parse("net472") / NuGetFramework.ParseFrameworkName(...) for legacy
```

**Pipeline**
```csharp
var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(256)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleWriter = true
});
// producer: await channel.Writer.WriteAsync(path, ct);  finally: channel.Writer.Complete(ex);
// workers:  await Parallel.ForEachAsync(channel.Reader.ReadAllAsync(ct),
//               new ParallelOptions { MaxDegreeOfParallelism = n, CancellationToken = ct }, ...)
// results:  a second bounded channel → one consumer that groups per app
```

**Async stream**
```csharp
async IAsyncEnumerable<ProjectFacts> ScanAsync(string root,
    [EnumeratorCancellation] CancellationToken ct = default)
```

**JSON**
```csharp
[JsonSerializable(typeof(AppFacts))]
partial class FactsJsonContext : JsonSerializerContext;
// await JsonSerializer.SerializeAsync(fileStream, facts, FactsJsonContext.Default.AppFacts, ct);
```

**Solutions:** the package `Microsoft.VisualStudio.SolutionPersistence` reads `.sln` and `.slnx`.

**Ctrl+C:** `Console.CancelKeyPress` → `e.Cancel = true; cts.Cancel();`
