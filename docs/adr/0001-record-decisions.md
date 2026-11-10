# 0001 — Record decisions as ADRs

Date: 2026-11-10
Status: Accepted

## Context
In 3 months I will not remember why the code is this way.

## Decision
Each decision that someone can ask "why?" about gets one file in `docs/adr/`.
Name: `NNNN-short-title.md`. Length: 5–10 lines. An ADR is never edited later;
a new ADR replaces it and the old one gets `Status: Superseded by NNNN`.

## Consequences
One more small file per decision. The history of "why" stays in the repo.
