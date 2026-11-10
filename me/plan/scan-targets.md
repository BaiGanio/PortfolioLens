# Scan targets — what we scan and why

All go into `scan-input/` (git-ignored). One sub-folder = one app.

I checked on 2026-11-10 that each public repo and each tag below exists. I did **not** open each repo to confirm its content. The "what to expect" column is from memory. Check it with your first scan; a surprise is a good test case.

## Set 1 — Legacy (.NET Framework). The scanner must shout here.

| Repo | Ref | What to expect | Why it is in the set |
|---|---|---|---|
| `dotnet-architecture/eShopModernizing` | default | ASP.NET MVC 5, WebForms, WCF N-tier, old-style csproj | Made by Microsoft as a "before modernization" sample. Best single legacy input |
| `nopSolutions/nopCommerce` | tag `release-3.90` | ASP.NET MVC 5, `packages.config`, many projects | Real product, real size. Pair with the modern tag below |
| `OrchardCMS/Orchard` | default | Orchard 1, MVC 5, 100+ old-style projects | Many legacy projects in one repo. Scale test for the legacy reader |
| `dnnsoftware/Dnn.Platform` | default | WebForms, old-style projects, some VB | WebForms + mixed languages. Closest to "ugly enterprise code" |
| `umbraco/Umbraco-CMS` | tag `release-7.15.9` | .NET Framework CMS | Pair with a modern Umbraco tag if you want one more before/after |
| `NuGet/NuGetGallery` | default | .NET Framework web app still in production | Shows that "legacy" also means "live and maintained" |
| `dotnet/samples` | default, folder `framework/wcf` only | Many small WCF samples | WCF rule test on real code. Copy only this folder |

## Set 2 — Modern. The scanner must stay quiet here.

| Repo | Ref | What to expect | Why |
|---|---|---|---|
| `dotnet/eShop` | default | Latest .NET, Aspire, **Central Package Management** | Tests `Directory.Packages.props`. Must score high |
| `NimblePros/eShopOnWeb` | default | Clean modern monolith | Small, easy first target |
| `nopSolutions/nopCommerce` | tag `release-4.90.8` | Modern .NET | The "after" of nopCommerce 3.90 |
| `OrchardCMS/OrchardCore` | default | 200+ SDK-style projects | Scale test: DB bulk path, token size in Phase 5 |
| `CoreWCF/CoreWCF` | default | Modern code that uses `System.ServiceModel` names | **False-positive test** for the WCF rule |
| `jbogard/ContosoUniversityDotNetCore` | default | Small modern app | Fast smoke test |

## Set 3 — Stress. Only for performance numbers.

| Repo | Why |
|---|---|
| `dotnet/roslyn` | Very large tree, thousands of `.cs` files, heavy use of props files and properties. Memory and time test. Expect many "unresolved property" errors — this is correct behavior |

## Set 4 — Your own repos (already on disk)

The list is private. It is in `me/private/scan-targets-own-repos.md`, which is not in git.

Do not copy the repos. Give the CLI more than one input folder. (Trap: Phase 1 says "do not follow symlinks" — so make the CLI accept several input paths.)

Rule from the master plan: results from private repos stay local. Not in git, not in the demo video.

## Clone commands

```bash
mkdir -p scan-input && cd scan-input

git clone --depth 1 https://github.com/dotnet-architecture/eShopModernizing
git clone --depth 1 --branch release-3.90   https://github.com/nopSolutions/nopCommerce nopCommerce-3.90
git clone --depth 1 --branch release-4.90.8 https://github.com/nopSolutions/nopCommerce nopCommerce-4.90
git clone --depth 1 https://github.com/OrchardCMS/Orchard
git clone --depth 1 https://github.com/dnnsoftware/Dnn.Platform
git clone --depth 1 --branch release-7.15.9 https://github.com/umbraco/Umbraco-CMS Umbraco-7
git clone --depth 1 https://github.com/NuGet/NuGetGallery
git clone --depth 1 https://github.com/dotnet/eShop
git clone --depth 1 https://github.com/NimblePros/eShopOnWeb
git clone --depth 1 https://github.com/OrchardCMS/OrchardCore
git clone --depth 1 https://github.com/CoreWCF/CoreWCF
git clone --depth 1 https://github.com/jbogard/ContosoUniversityDotNetCore

# only one folder of a big repo
git clone --depth 1 --filter=blob:none --sparse https://github.com/dotnet/samples dotnet-samples-wcf
git -C dotnet-samples-wcf sparse-checkout set framework/wcf

# stress test — big download, do it later
git clone --depth 1 https://github.com/dotnet/roslyn
```

`--depth 1` gives no history. For the optional git-facts rule (Phase 3G), clone 2–3 repos again with full history.

## Which set for which phase

| Phase | Use |
|---|---|
| 1 | Fixtures for tests. Then eShopOnWeb → eShop → eShopModernizing → all. Roslyn repo for the memory test |
| 2 | All. OrchardCore for the bulk-path numbers |
| 3 | eShopModernizing + nopCommerce pair for the rules. CoreWCF for false positives |
| 4 | Zip 10–20 of them for the load test. One broken zip. One zip bomb (make it yourself, small) |
| 5 | 5 apps for the eval: eShopModernizing, nopCommerce-3.90, nopCommerce-4.90, eShop, Dnn.Platform |
| 6 | 30 jobs from any of them |

## Things the real product scans that we do not (know them, do not build them)

Say these if they ask "what is missing?":

- Database schemas (tables, stored procedures, size) from a live connection or a DACPAC.
- Other languages: Java, COBOL, VB6, JavaScript. One parser per language, one common model.
- Binaries without source (IL inspection with `System.Reflection.Metadata`).
- Runtime data: who calls whom in production, from logs or APM.
- Infrastructure: IIS settings, Windows services, scheduled tasks.
- Licences of the dependencies.
- Business data: owner, cost, criticality — from a CMDB or a spreadsheet import.

## Fixtures (hand-made, inside the repo, public)

Real repos change. Tests need fixed input. Full list across the phases:

`sdk-style-net10`, `old-style-net472`, `multi-target`, `central-packages`, `props-inherit`, `unresolved-property`, `broken-xml`, `vbproj-legacy`, then one folder per rule: `rule-PL2001-system-web`, `rule-PL2002-wcf`, `rule-PL2003-binaryformatter`, … each with `positive.cs` and `negative.cs`.
