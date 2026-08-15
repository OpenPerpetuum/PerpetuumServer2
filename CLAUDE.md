# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# What This Is

Open Perpetuum Server 2 is an MMO game server for the Perpetuum online game.

Technology:
- .NET 8
- C# 12
- SQL Server
- x64 only
- Windows only

---

# Instruction Priority

When instructions conflict, prioritize:

1. Correctness and safety
2. Existing architecture consistency
3. Database/documentation accuracy
4. Runtime stability and performance
5. Minimal change scope
6. Coding style consistency

---

# Core Engineering Principles

- Don't assume. Surface uncertainty explicitly.
- Prefer minimal, focused changes.
- Touch only what is necessary.
- Preserve existing architecture and runtime assumptions.
- Reuse existing patterns before introducing new abstractions.
- Prefer consistency over novelty.
- Avoid speculative refactors.
- Keep naming and placement consistent with surrounding code.
- Do not make commits unless explicitly asked.

Update:
- `docs/codebase/ARCHITECTURE.md`

when introducing major architectural changes.

---

# Build & Run

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64

cd src/Perpetuum.Server
dotnet run -- "E:\PerpetuumServer2\data"
```

CI:
- `.github/workflows/dotnet.yml`

Output:
- `bin/x64/Release/net8.0`

There are currently no automated tests.

---

# Authoritative Documentation

The `docs/` directory is the authoritative source of truth.

## Architecture
- `docs/codebase/ARCHITECTURE.md`

## Technical Concerns
- `docs/codebase/CONCERNS.md`

## Coding Conventions
- `docs/codebase/CONVENTIONS.md`

## Integrations
- `docs/codebase/INTEGRATIONS.md`

## Technology Stack
- `docs/codebase/STACK.md`

## Project Structure
- `docs/codebase/STRUCTURE.md`

## Testing Constraints
- `docs/codebase/TESTING.md`

---

# Database Source of Truth

Database documentation under `docs/db_structure/` is authoritative.

## Core Schema
- `docs/db_structure/database_schema_documentation.md`

## Stored Procedures
- `docs/db_structure/stored_procedures/*.sql`

## Functions
- `docs/db_structure/functions/*.sql`

## Views
- `docs/db_structure/views/*.sql`

## User-defined Data Types
- `docs/db_structure/data_types/*.sql`

Claude MUST:
- verify schema before generating SQL
- verify joins before writing queries
- verify procedures/functions/views before introducing new SQL
- avoid hallucinating schema objects
- prefer existing DB patterns
- preserve actual SQL types and nullability

---

# Game Content Creation

When creating or modifying gameplay entities (items, robots, effects, modules, tech tree nodes), Claude MUST consult:

- `docs/content/claude_game_content_guide.md`

This guide is the authoritative procedural reference for SQL content pipelines and dependency order.

## Content Creation Rules

Claude MUST:

- Read the guide before generating any content SQL.
- Follow the entity lifecycle and dependency order defined in the guide (sections 2 and 24).
- Never hardcode definition or extension IDs — always resolve dynamically via `entitydefaults` / `extensions` lookups.
- Use naming conventions from the guide (section 3): `def_`, `_pr`, `_cprg`, `effect_`, `cf_` prefixes.
- Use idempotent SQL patterns: `MERGE`, `IF NOT EXISTS`, or `DELETE + INSERT` as appropriate per table.
- Generate full-chain content when possible — avoid partial generation.
- Run the validation checklist (section 26) before declaring content complete.
- Ask the user for existing database values when dynamic resolution requires live data not available in docs.

Claude MUST NOT:

- Hardcode IDs for definitions, extensions, aggregate fields, or tech tree nodes.
- Assume table relationships without verifying via `docs/db_structure/`.
- Generate partial content chains that leave items in an unresearchable, uncraftable, or inaccessible state.

---

# Required Workflow

For any non-trivial task:

1. Identify affected subsystems
2. Identify relevant documentation
3. Locate similar implementations
4. Check `docs/graph/GRAPH_REPORT.md` for God Nodes (high-risk symbols); run `.\tools\query-graph.ps1 <ClassName> -Direction in` to enumerate direct dependents — a null result is normal (most classes have no detected importers) and does not mean the change is safe (if `graph.json` is absent, skip and continue to step 5)
5. Understand existing patterns
6. Evaluate runtime implications
7. Produce a short implementation plan — for any task that modifies an interface or a widely-used class, the plan must include an explicit step to run `.\tools\query-graph.ps1 <ClassName> -Direction in` before touching that file
8. Then implement

---

# Architectural Rules

## Dependency Injection

New code should use constructor injection.

Avoid expanding legacy static service locator patterns unless compatibility requires it.

## Request Handlers

Client commands must follow the existing handler architecture:
- command registration in `Commands.cs`
- handler in `Perpetuum.RequestHandlers`
- Autofac registration

Handlers should remain thin orchestration layers.

Business logic belongs in services/domain systems.

## Zone Safety

Respect the single `ProcessManager` loop architecture.

Avoid:
- blocking operations inside zone updates
- `.Result` / synchronous task waits
- long synchronous DB operations in hot paths
- unsafe shared-state mutation

## Database Access

Prefer existing subsystem patterns.

Use repositories where they already exist.

Avoid:
- unsafe SQL interpolation
- `SELECT *`
- duplicated SQL logic
- schema assumptions

## Error Handling

Use existing patterns:
- `PerpetuumException`
- `ErrorCodes`
- `ThrowIf*` guard extensions

---

# Technical Debt Rules

Avoid worsening known technical debt documented in:
- `docs/codebase/CONCERNS.md`

Avoid:
- new static service locators
- new magic constants
- unsafe SQL patterns
- fire-and-forget async without cancellation
- new `#if DEBUG` behavioral divergence

---

# Modification Rules

When modifying existing systems:
- preserve public contracts
- preserve serialization compatibility
- preserve DB compatibility
- preserve network protocol compatibility
- preserve threading assumptions
- preserve runtime behavior

Avoid broad refactors unless explicitly requested.

---

# Performance Rules

Evaluate runtime impact before introducing:
- LINQ in hot paths
- blocking waits
- excessive allocations
- immutable collection churn
- synchronous DB work in update loops

High-risk hot paths include:
- zone updates
- NPC AI
- combat
- movement
- market processing
- season activity tracking

---

# Security Rules

Never:
- introduce plaintext credentials
- weaken authentication
- bypass access validation
- introduce unsafe SQL construction

Prefer:
- parameterized queries
- existing auth flows
- existing validation patterns

---

# Testing & Validation

There is currently no automated test suite.

Claude MUST:
- propose manual validation steps
- identify affected gameplay systems
- identify affected DB state
- identify likely regression areas

---

# Response Expectations

For implementation tasks, provide:

1. Affected systems
2. Relevant files/docs consulted
3. Risks and constraints
4. Implementation plan
5. Code changes
6. Manual validation steps
7. Potential regressions

For DB-related tasks:
- mention consulted tables/views/procedures/functions
- explain important relationship paths when relevant

Avoid generating code before analysis.

---

# Code Placement

Before creating files:
- verify correct subsystem placement in `docs/codebase/STRUCTURE.md`
- follow existing namespace patterns
- follow existing folder organization

Avoid parallel abstractions unless justified.

---

# AI Contribution Rules

## Where to Edit

| Purpose | File |
|---|---|
| Main AI instructions | `CLAUDE.md` |
| Architecture deep-dive | `docs/codebase/ARCHITECTURE.md` |
| Codebase graph & impact analysis | `.claude/knowledge/codebase-graph.md` |
| Specialist agents | `.claude/agents/<name>.md` |


---

# Backlog Management

The project board is authoritative project memory.

## Where the backlog lives

https://github.com/orgs/OpenPerpetuum/projects/6

The backlog was kept in `docs/backlog/*.md` until 2026-08-14. All 85 entries were moved to the board
as draft issues, keeping their `ISSUE-NNN` / `IMPROVEMENT-NNN` identifiers in the item title. The
files were removed so there is one place to read and one place to update; earlier revisions remain in
git history.

Reading the board needs the `read:project` scope, which a fresh `gh` token does not carry:

```bash
gh auth refresh -h github.com -s project
```

## Backlog Rules

Claude MUST:
- review the board before major implementation work
- avoid duplicate items
- update the related item after implementation
- preserve item identifiers — the `ISSUE-NNN` / `IMPROVEMENT-NNN` prefix in the title is how entries
  are cross-referenced from commits, pull requests and the implementation plans under
  `docs/superpowers/plans/`
- prefer updating an existing item over creating a duplicate
- keep items concise and structured
- set an item's Status to `Done` when it is complete, rather than moving it anywhere

When asked to:
- "work on backlog"
- "pick a task"
- "continue work"
- "fix issues"
- "implement improvements"

Claude should:
1. review the board, filtering to what you have been asked about (issues or improvements), unless the two depend on each other
2. prioritize unfinished HIGH priority items
3. prefer low-risk/high-impact work unless instructed otherwise
4. produce a short implementation plan
5. update the item's Status after work completes

## Backlog Statuses

The board's `Status` field, in order:

- BACKLOG
- Triage
- Checkup
- TODO
- In progress
- Review in progress
- Reviewer approved
- Done

The file-based backlog also used `BLOCKED` and `DEFERRED`, which the board has no equivalent for.
Both were imported as `BACKLOG`; state the distinction in the item body when it matters.

## Backlog Priorities

The board has no priority field. Priority stays in the item body, on its own line, using:

- CRITICAL
- HIGH
- MEDIUM
- LOW

## Recommended Backlog Item Format

Item title:

```
ISSUE-001 - Short title
```

Item body:

```md
Priority: HIGH
Area: Networking

### Problem
Concise issue description.

### Impact
Runtime/gameplay/maintenance impact.

### Proposed Fix
Short implementation direction.

### Notes
Optional additional context.
```

Status is the board field, not a line in the body. Priority stays in the body because the board has
no field for it.

