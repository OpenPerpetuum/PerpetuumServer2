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
- `.claude/knowledge/architecture.md`

when introducing major architectural changes.

---

# Build & Run

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64

cd src/Perpetuum.Server
dotnet run -- --GameRoot "E:\PerpetuumServer2\data"
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
- `docs/CONCERNS.md`

## Coding Conventions
- `docs/CONVENTIONS.md`

## Integrations
- `docs/INTEGRATIONS.md`

## Technology Stack
- `docs/STACK.md`

## Project Structure
- `docs/STRUCTURE.md`

## Testing Constraints
- `docs/TESTING.md`

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
4. Understand existing patterns
5. Evaluate runtime implications
6. Produce a short implementation plan
7. Then implement

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
- `docs/CONCERNS.md`

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
- verify correct subsystem placement in `docs/STRUCTURE.md`
- follow existing namespace patterns
- follow existing folder organization

Avoid parallel abstractions unless justified.

---

# AI Contribution Rules

## Where to Edit

| Purpose | File |
|---|---|
| Main AI instructions | `CLAUDE.md` |
| Architecture deep-dive | `.claude/knowledge/architecture.md` |
| Specialist agents | `.claude/agents/<name>.md` |


---

# Backlog Management

Persistent project backlog files are authoritative project memory.

## Backlog Files

Primary:
- `docs/backlog/issues.md`
- `docs/backlog/improvements.md`

Optional:
- `docs/backlog/active-sprint.md`
- `docs/backlog/completed.md`

## Backlog Rules

Claude MUST:
- review backlog files before major implementation work
- avoid duplicate backlog entries
- update related backlog items after implementation
- preserve backlog structure and identifiers
- prefer updating existing items over creating duplicates
- keep backlog entries concise and structured
- move completed items to `completed.md` when appropriate

When asked to:
- "work on backlog"
- "pick a task"
- "continue work"
- "fix issues"
- "implement improvements"

Claude should:
1. review backlog files, only check what you've been asked to, (e.g. issues or improvements), unless issues and improvements are depending on each other
2. prioritize unfinished HIGH priority items
3. prefer low-risk/high-impact work unless instructed otherwise
4. produce a short implementation plan
5. update backlog status after work completes

## Backlog Statuses

Use:
- TODO
- IN_PROGRESS
- BLOCKED
- DONE
- DEFERRED

## Backlog Priorities

Use:
- CRITICAL
- HIGH
- MEDIUM
- LOW

## Recommended Backlog Entry Format

```md
## ISSUE-001 - Short title

Status: TODO
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

