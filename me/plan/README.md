# PortfolioLens — Master Plan

Start here. This folder is public: it is in git. Private notes go in `me/private/`, which is in `.gitignore`. Never put an employer, a salary, an interview detail, or a private repo name in this folder.

| File | What it is |
|---|---|
| `phase-0-setup.md` … `phase-6-onprem.md` | One file per phase: steps, "done when" checks, traps, hints |
| `scan-targets.md` | Which repos to scan, and why each one |
| `README.md` (this) | Decisions, open questions, progress |

## How each phase file works

The phase files are an **outline** now. We extend each one in its own session, when you start that phase (rules: `../../CLAUDE.md`).

Say "start phase N". I then ask the open questions, and I fill the file **before you code** with:

0. **The story** — where we are, the problem, real-world examples, what we build, why this way.
1. **Diagram** — Mermaid, of what the phase builds.
2. **Steps** — small, in order.
3. **Commands** — exact, per step.
4. **Code examples** — one per new idea, with the reason.
5. **Traps** — next to the step where they happen.
6. **Done when** — checks you can run. All green = phase done.
7. **Say it in the interview** — the sentence this phase gives you.

Then you write the code by hand. At the end, I review the diff.

You make the commits. I give you only the commit message text. I do not push.

At the end of each phase, I write the public chapter in `docs/story/phase-N-*.md` (one file per phase). It has no employer, no interview, and no private repo data. You approve it.

After a phase with real challenges, I add a block "Challenges — Phase N" to the private file `me/private/interview-qa.md`: the problem, what you tried, what solved it, the lesson.

## Decisions already made (2026-11-10)

| Topic | Decision |
|---|---|
| Spec style | Steps + checks, plus hints at the bottom of each phase |
| Azure | Local emulators first, then one real deploy with Bicep, then delete |
| LLM | Azure AI Foundry is the main one. Ollama is the on-prem swap |
| Scan input | Public repos + own local repos (the list is private) |
| Order | 0 → 1 → 2 → 3 → 4 → 5 → 6 (no deadline) |
| Runtime | .NET 10 (LTS, supported to Nov 2028). SDK 10.0.403 is installed |
| Solution file | `.slnx` (the .NET 10 default) |
| Tests | xUnit v3 on Microsoft Testing Platform |
| Analyzers | `AnalysisLevel=latest-recommended` + `EnforceCodeStyleInBuild` |
| `CLAUDE.md` | Committed (public) |
| Repo visibility | Public at the end of Phase 0 |
| Git flow | One branch + one pull request per phase |

Tools found on your Mac: .NET SDK 10.0.403, Docker 29.8.2, Azure CLI.

## Solution layout (proposal)

```
PortfolioLens.slnx
src/
  PortfolioLens.Core         model + interfaces (no dependencies)
  PortfolioLens.Extraction   file walk, csproj/sln/config readers   (Phase 1)
  PortfolioLens.Analysis     Roslyn walkers + IRule engine          (Phase 3)
  PortfolioLens.Data         EF Core, migrations, upsert            (Phase 2)
  PortfolioLens.Api          Minimal API                            (Phase 2)
  PortfolioLens.Cli          console entry point                    (Phase 1)
  PortfolioLens.Worker       Azure Functions isolated               (Phase 4)
  PortfolioLens.Ai           LLM summary, cache, cost log           (Phase 5)
  PortfolioLens.Mcp          MCP server                             (Phase 5)
tests/
  PortfolioLens.UnitTests
  PortfolioLens.IntegrationTests
fixtures/                    tiny hand-made projects, one per smell
infra/                       Bicep + docker-compose
docs/adr/                    short decision notes
```

Rule: `Core` references nothing. Everything else points inward to `Core`.

## Rules for the whole project

- Nullable on. Warnings as errors. `.editorconfig`. Central Package Management (`Directory.Packages.props`).
- `CancellationToken` on every async method. No `.Result`, no `.Wait()`.
- One bad file never stops a scan. Log it, count it, continue.
- Every decision that you can be asked "why?" about → one ADR in `docs/adr/` (5–10 lines).
- CI from day one: GitHub Actions runs build + tests on each push.
- Scan output (JSON, DB dumps) never goes into git. The repo is public; facts from private repos are not.

## Open questions — I need your answer (my default is in bold)

I use the default if you say nothing.

1. **What is one "App"?** A repo can have many `.sln` files.
   → **One repo = one App.** Solutions are stored as a property. Simple and good enough.
2. **How do we read `.csproj`?** Plain XML is fast and needs no build. MSBuild evaluation is exact but slow and breaks on old projects.
   → **Plain XML**, plus manual support for `Directory.Packages.props` and `Directory.Build.props`. Write the limits in an ADR.
3. **Do we keep history?** Overwrite on each scan, or keep what changed?
   → **Current state + `FirstSeenRunId` / `LastSeenRunId` on findings.** You can then show "fixed since last scan". Cheap to build.
4. **VB.NET and F# projects?** Dnn.Platform and old enterprise code have `.vbproj`.
   → **Project files: yes** (same XML). **Roslyn rules: C# first**, VB later only if time.
5. **UI?** → **No UI.** API + one generated HTML/Markdown report per app. UI costs days and it is not the goal of this project.
6. **k3s homelab is a Raspberry Pi 5 (ARM64)?** SQL Server has no ARM64 Linux image.
   → **Phase 6: app in k3s, SQL Server outside** (on the Mac or an x86 box). Second option: add PostgreSQL as a second EF provider. That is more work, but it is a real "support matrix" story. Decide when you reach Phase 6.
7. **How do you want to use AI while you code by hand?**
   → **You write; AI reviews.** After each phase: run a code review with Claude Code, fix, and write 3 lines in `docs/ai-log.md` (what AI found, what you rejected, why). This log shows a structured use of AI in the work.
8. **When does the GitHub repo go public?** → **From the start.** Commit history that grows in small steps looks real.

## My suggestions (not in your first plan)

- **Phase 0 added.** Setup, CI, fixtures, clone script. Half an evening.
- **`fixtures/` folder.** Tiny fake projects, one per smell (old csproj, `packages.config`, WCF, `BinaryFormatter`…). Tests use these, so tests are fast and exact. Real repos are for scale only.
- **Config and file-type scan (Phase 3).** `web.config`, `*.svc`, `*.aspx`, `*.edmx`, `Global.asax`. Cheap to build. Real portfolio tools do this.
- **Git facts (Phase 3, optional).** Last commit date, number of authors. "Old and nobody touches it" is a real risk signal.
- **Support dates from data, not from code.** Use Microsoft's `releases-index.json`. Keep a copy in the repo for offline use (on-prem story).
- **Readiness score per app (Phase 3).** One number 0–100 from the findings. Managers want one number.
- **Same repo, two ages.** Scan nopCommerce 3.90 (.NET Framework) and 4.90 (modern). Your demo shows the "before and after".
- **Snapshot tests** (Verify library) for extractor JSON. Small effort, big safety.
- **Zip safety (Phase 4).** Zip bomb and "zip slip" path checks.
- **Budget alert in Azure** before the first deploy. €10 limit.

## Progress

- [ ] Phase 0 — Setup
- [ ] Phase 1 — Extractor
- [ ] Phase 2 — Storage + API
- [ ] Phase 3 — Roslyn + rules
- [ ] Phase 4 — Azure pipeline
- [ ] Phase 5 — AI layer
- [ ] Phase 6 — On-prem mode
