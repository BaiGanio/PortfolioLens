# AI log

After each phase, an AI assistant reviews the diff. This file records 3 things per phase: what the review found, what the owner rejected, and why.

## Phase 0 — Setup (2026-11-10)

- **Found:** The code was good: 0 warnings, 1 test green, CI green. But private notes were in an old commit, and a deleted file stays in the git history. An ignore rule also showed a private folder name.
- **Rejected:** The advice to keep the whole plan folder out of git. Also the note about a BOM at the start of 4 files.
- **Why:** The owner reads the plan on the web, so the plan is public and only the private notes are ignored. The history got a fresh start in a new repo. The BOM does no harm.
