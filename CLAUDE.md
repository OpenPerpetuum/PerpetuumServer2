# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

Open Perpetuum Server 2 is an MMO game server for the Perpetuum online game. It manages player sessions, zones (game world instances), combat, robot equipment modules, NPCs, missions, market trading, crafting, and player-built structures. Built on .NET 8, targeting x64 Windows only.

## Change Guidelines

- Don't assume. Don't hide confusion. Surface tradeoffs.
- Minimum code that solves the problem. Nothing speculative.
- Touch only what you must. Clean up only your own mess.
- Define success criteria. Loop until verified.
- Prefer minimal, focused changes - do not refactor unrelated areas.
- Mirror existing folder and file patterns when adding features.
- Keep naming consistent with surrounding code.
- Update [`.claude/knowledge/architecture.md`](.claude/knowledge/architecture.md) when introducing major architectural changes.
- Don't make any code commits unless explicitly asked.

## Build & Run

```bash
# Build (Release x64)
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64

# Run as console app (requires a configured GameRoot with perpetuum.ini and database)
cd src/Perpetuum.Server
dotnet run -- --GameRoot "E:\PerpetuumServer2\data"
```

CI builds via `.github/workflows/dotnet.yml` — outputs to `bin/x64/Release/net8.0`.

There are **no automated tests** in this repository.

## Configuration

The server reads two config files:

- **`appsettings.json`** (in `Perpetuum.ServerService2/`) — sets `GameRoot` path and .NET logging
- **`perpetuum.ini`** (inside GameRoot directory) — SQL Server connection string, ports, zone config, feature flags, stored as `GlobalConfiguration` JSON

Server startup sequence: `Perpetuum.Server` → `PerpetuumBootstrapper.Init(gameRoot)` → loads 18+ Autofac modules → connects to SQL Server → loads entity definitions → `bootstrapper.Start()` → host transitions Init → Starting → Online.

## Database Source of Truth

The database structure documentation located under `docs/db_structure` is the authoritative source of truth for all database-related work.

You MUST always consult these files before:
- generating SQL
- modifying queries
- designing repositories/services
- creating DTOs/models
- writing migrations
- proposing schema changes
- analyzing performance issues
- reasoning about relationships
- generating API contracts involving database entities

Never assume table structures, column names, relationships, data types, constraints, indexes, views, functions, or stored procedure signatures from memory.

For every database-related task, begin by identifying which files under `docs/db_structure` are relevant before producing the final answer.

Before writing any JOIN:
- verify the relationship exists in schema documentation
- identify the exact join keys
- explain the join path internally before generating SQL

Do not load the entire schema documentation unnecessarily.
Only retrieve and inspect entities directly relevant to the current task.

### Documentation Structure

#### Core Schema
- `docs/db_structure/database_schema_documentation.md`
  - Contains tables
  - Columns
  - Relations
  - Indexes
  - Primary/foreign keys

#### Stored Procedures
- `docs/db_structure/stored_procedures/*.sql`
  - One stored procedure per file
  - Filename matches procedure name

#### Functions
- `docs/db_structure/functions/*.sql`
  - One function per file
  - Filename matches function name

#### Views
- `docs/db_structure/views/*.sql`
  - One view per file
  - Filename matches view name
  
#### User-defined data types
- `docs/db_structure/data_types/*.sql`
  - One view per file
  - Filename matches view name

### Mandatory Behavior

When working with database-related tasks:

1. FIRST search relevant files in `docs/db_structure`
2. THEN generate or analyze code
3. Prefer existing stored procedures/functions/views over inventing new SQL
4. Reuse existing naming conventions and patterns
5. Validate all joins and field names against documentation
6. Never hallucinate schema objects
7. If information is missing from documentation:
   - explicitly state what is missing
   - avoid guessing

### SQL Generation Rules

Before generating SQL:
- verify table existence
- verify column existence
- verify join relationships
- verify parameter names/types for procedures/functions
- verify view definitions

If an object already exists as:
- stored procedure
- function
- view

prefer using or extending it instead of duplicating logic.

### Architecture Rules

When generating backend code:
- derive models from documented schema
- preserve actual nullability
- preserve actual SQL types
- preserve real relationships
- preserve naming conventions exactly

Never rename fields unless explicitly requested.

### Performance Rules

When analyzing performance:
- inspect indexes from schema docs
- prefer indexed joins/filtering
- avoid assumptions about clustered keys
- check existing views/functions/procedures before proposing alternatives

### Conflict Resolution

If generated code conflicts with documentation:
- documentation wins
- do not trust prior conversation context over documentation

### Output Expectations

For database-related answers:
- mention which entities were consulted
- reference exact tables/views/procedures/functions used
- explain relationship paths when relevant

## Architecture

### Project Layout

| Project | Role |
|---|---|
| `Perpetuum` | Core library — all game logic |
| `Perpetuum.Bootstrapper` | Autofac DI wiring, one module per subsystem |
| `Perpetuum.RequestHandlers` | 150+ command handler classes |
| `Perpetuum.Server` | Console entry point |
| `Perpetuum.ServerService2` | Windows service wrapper |
| `Perpetuum.ExportedTypes` | Shared type definitions |

### Dependency Injection (Autofac)

Everything is wired in `Perpetuum.Bootstrapper/Modules/`. Each major system has its own Autofac module (zones, missions, entities, NPCs, market, etc.). Adding a new service means: implement the class, register it in the appropriate module, and inject it via constructor.

### Command/Request Handler Pattern

```
Client → TCP → Session → Command dispatch → IRequestHandler → Response
```

All ~200+ commands are defined in `Commands.cs` with text name, access level, and argument schema. Each command maps to a handler class in `Perpetuum.RequestHandlers/`. To add a new command: define it in `Commands.cs`, create a handler class, register it in the bootstrapper.

### Entity System

- `Entity` — base game object with `Eid` (entity ID) and dynamic property bag
- `EntityDefault` — template/definition data loaded from the database
- `OptionalProperty<T>` / `DynamicProperty<T>` — typed property accessors on entity instances
- Entities are not ORM-mapped rows; they carry runtime state overlaid on definition data

### Zones

Each `IZone` is a self-contained simulation: terrain grid, units (players/robots/NPCs), environmental effects, locking, and combat. Zones run in parallel. Each zone has its own network listener port. Key files: `src/Perpetuum/Zones/` (35 subdirectories) including `NpcSystem/`, `Terrains/`, `PBS/` (player bases), `Intrusion/`.

### Modules System

Robot equipment = `Module` objects with state machines. `ActiveModule.States.cs` manages state transitions. Modules track ammo, energy consumption, and heat. Module types live in `src/Perpetuum/Modules/` (40+ files).

### Network & Serialization

- `Perpetuum.Network` — TCP connections with encryption
- `Perpetuum.GenXY` — custom binary wire format; use `GenxyReader`/`GenxyWriter` for protocol I/O, `GenxyConverter` to register custom type serialization

### Services

Specialized long-running services under `src/Perpetuum/Services/`:
- `MissionEngine` — mission progression and rewards
- `MarketEngine` — item trading and pricing
- `ProductionEngine` — crafting/manufacturing
- `Sessions` — player session management
- `EventServices` — world events and NPC spawning
- `Standing` — faction relationships

## Contributing AI Instructions

### Where to edit

| You want to change... | Edit this file |
|-----------------------|----------------|
| Project context (this file) | `CLAUDE.md` |
| Architecture deep-dive | `.claude/knowledge/architecture.md` |
| A specialist agent | `.claude/agents/<name>.md` |