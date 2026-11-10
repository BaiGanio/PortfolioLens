# PortfolioLens — rules for Claude

PortfolioLens scans .NET repos, stores the facts, flags modernization risks, and explains them.
This is a learning project. **The owner writes the code by hand.**

## Who does what

- The owner writes all code in `src/`, `tests/`, `infra/`, and `fixtures/`.
- Claude prepares, explains, and reviews.
- Claude does **not** create or edit files in those folders, unless the owner asks for it in that message.
- Claude can write freely in `me/plan/` and `docs/`.

## Plan files

- The plan is in `me/plan/`. Start with `me/plan/README.md`. It holds the decisions and the progress list.
- `me/plan/` is public. It is in git. Never put an employer, a client, a salary, an interview detail, or a private repo name in it.
- `me/private/` is git-ignored and private. Never copy its content into a tracked file, a commit message, or a pull request.
- One phase = one file: `me/plan/phase-N-*.md`.

## How a phase session works

When the owner says "start phase N":

1. Read `me/plan/README.md` and the phase file. Read the code that exists.
2. Ask the open questions for this phase first. Maximum 2 options per question, with a recommendation.
3. **Extend the phase file before the owner writes any code.** Put all of this in it, up front:
   - the story of the phase (see "The project is a story")
   - a diagram of what the phase builds (see "Diagrams")
   - each step, in order, small
   - the exact commands for each step (`dotnet new`, `dotnet add package`, `docker`, `az`, `git`)
   - a code example for each new idea: real, complete enough to learn from, with the reason for each choice
   - the package names, with versions checked against the current documentation
   - the traps, next to the step where they happen
   - the commit message at the end of each step that ends in a commit, ready to copy
   - the "done when" checks
4. Then stop. The owner writes the code.
5. During the work: answer questions, explain errors, give a hint. Do not paste a full solution unless asked.
6. At the end: review the diff. Then add 3 lines to `docs/ai-log.md` (what the review found, what the owner rejected, why).
7. Update the progress list in `me/plan/README.md`.
8. If the phase had real challenges, add them to `me/private/interview-qa.md` in a block "Challenges — Phase N". Each one has: the problem, what the owner tried, what solved it, the lesson. Use the owner's real numbers and real errors. Add new questions that came up too.

9. Write the chapter in `docs/story/` (see "The public story").

Do not extend a later phase early. The plan changes as the code grows.

## The project is a story

Each phase is one chapter. The owner must know the problem before the solution.

Each phase file starts with a section "The story". It has, in short sentences:

- **Where we are:** what the last phase gave us, and what still hurts.
- **The problem:** what goes wrong without this phase.
- **Real world:** 1–2 examples from real companies or real systems where this problem occurs. Real, not invented. If you are not sure that an example is true, do not use it.
- **What we build:** the solution, in plain words.
- **Why this way:** the reason for the main choice, and what we did not choose.

Each step in the phase also gets one line of "why" before its commands and code.
Keep it short. A story of 10–15 lines is enough.

### The public story: `docs/story/`

- At the end of each phase, Claude writes the chapter for the phase and the owner approves it. This is one of the files that Claude can write.
- One phase = one file: `docs/story/phase-N-*.md`, with the same name as the plan file. `docs/story/README.md` is the index: add one line with a link for each new chapter.
- Write it after the work, so it tells what happened, not what was planned.
- Content: the problem, a real-world example, what was built, why this way, one diagram, and what was hard (with real numbers).
- It is public. Write it new for a stranger who opens the repo. Do not copy text from `me/private/`.
- Do not name an employer, a client, an interview, or a private repo. Do not put results from private repos in it.

## Diagrams

- Use a diagram where it helps to understand: data flow, pipeline stages, table relations, message sequence, state changes, deployment.
- Format: Mermaid in a fenced `mermaid` block, inside the Markdown file. It renders on GitHub and in the editor.
- Pick the type that fits: `flowchart` (flow), `sequenceDiagram` (messages), `erDiagram` (tables), `stateDiagram-v2` (status values).
- One idea per diagram. Small. No diagram for a thing that one sentence explains.

## Code examples and facts

- Check APIs, package versions, and Azure limits against the current documentation before you put them in a phase file. Do not write them from memory.
- If you could not check a fact, mark it with ⚠ and say so.
- Compile or run an example when you can. Say when you did not.

## Code rules (for examples and for reviews)

- .NET 10. Nullable on. Warnings are errors. Central Package Management.
- `CancellationToken` on each async method. No `.Result`, no `.Wait()`.
- `Core` references no other project. All other projects point to `Core`.
- One bad input file never stops a scan. Record the error and continue.
- No secrets in code or config. Azure access uses Managed Identity.
- Scan output never goes into git.
- Each decision that needs a "why" gets a short ADR in `docs/adr/`.
- Tests that touch SQL use a real SQL Server (Testcontainers). No mock of `DbContext`.

## Git

- The owner makes the commits. Claude does not run `git commit`.
- When a piece of work is ready, Claude gives **only the commit message** as text. The owner copies it.
- Message format: a short subject line in the imperative ("Add bounded channel to the scan pipeline"), then 1–3 lines that say why.
- Claude never pushes, unless the owner asks for it in that message.
