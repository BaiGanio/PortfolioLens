# 0003 — `.slnx` solution file, xUnit v3 on Microsoft Testing Platform

Date: 2026-11-10
Status: Accepted

## Context
A new .NET 10 repo must pick a solution format and a test stack.
The old choices (`.sln`, xUnit v2, VSTest) still work, but they are the old line.

## Decision
- Solution file: `.slnx`. It is the .NET 10 default. It is short XML, so a diff is easy to read.
- Tests: xUnit v3 on Microsoft Testing Platform (MTP). The test project is a normal executable.
  `global.json` has a `test` block that tells `dotnet test` to use MTP.

## Consequences
The scanner must read `.slnx` and `.sln`, because real repos have both.
Without the `test` block in `global.json`, `dotnet test` uses VSTest and does not run the tests correctly.
Old tools that know only `.sln` or VSTest do not work with this repo.
