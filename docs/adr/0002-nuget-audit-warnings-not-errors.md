# 0002 — NuGet audit warnings stay warnings

Date: 2026-11-10
Status: Accepted

## Context
`Directory.Build.props` sets `TreatWarningsAsErrors=true` for all projects.
`dotnet restore` also reports packages with known security holes (NU1901–NU1904).
A new advisory can appear on any day. Then the build fails with no code change.

## Decision
`WarningsNotAsErrors` lists NU1901, NU1902, NU1903, and NU1904.
All other warnings stay errors.

## Consequences
A new advisory does not block an unrelated fix. The warning still shows in each build and in CI.
The risk: nothing forces me to fix it. I must read the restore output and update the package.
