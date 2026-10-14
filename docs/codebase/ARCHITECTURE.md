<!-- refreshed: 2026-05-11 -->
# Architecture

**Analysis Date:** 2026-05-11

## System Overview

```text
┌──────────────────────────────────────────────────────────────────────┐
│                        Client (TCP)                                  │
└────────────────────────────┬─────────────────────────────────────────┘
                             │ Encrypted TCP (RC4/RSA handshake)
                             ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Relay / Session Layer                                               │
│  `src/Perpetuum/Services/Sessions/Session.cs`                        │
│  `src/Perpetuum/Network/ClientConnection.cs`                         │
│  Authn, command dispatch, character selection                        │
└────────────┬───────────────────────────────────────┬─────────────────┘
             │ IRequest                              │ IZoneRequest
             ▼                                       ▼
┌────────────────────────┐             ┌─────────────────────────────┐
│  IRequestHandler       │             │  IZoneSession / ZoneSession  │
│  (150+ handlers)       │             │  `src/Perpetuum/Zones/       │
│  `src/Perpetuum.       │             │   ZoneSession.cs`            │
│   RequestHandlers/`    │             └──────────────┬──────────────┘
└────────────────────────┘                            │
             │                                        │
             ▼                                        ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Zone (parallel simulation processes)                                │
│  `src/Perpetuum/Zones/Zone.cs`  (abstract)                           │
│  PveZone / PvpZone / StrongHoldZone / TrainingZone                   │
│  Each zone: own TCP listener port, terrain grid, units, NPC presences│
└──────────────┬───────────────────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Core Game Services (singletons, process-ticked)                     │
│  MarketEngine · MissionEngine · ProductionEngine · Seasons           │
│  Standing · Social · EventServices · ExtensionService · Sessions     │
│  `src/Perpetuum/Services/`                                           │
└──────────────┬───────────────────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────────────────┐
│  SQL Server (via Db static factory, System.Transactions)             │
│  `src/Perpetuum/Data/Db.cs`                                          │
└──────────────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

| Component | Responsibility | Key File |
|-----------|----------------|----------|
| `Session` | Per-client TCP connection, auth, command routing | `src/Perpetuum/Services/Sessions/Session.cs` |
| `IRequestHandler<T>` | Handles a single named command | `src/Perpetuum/Host/Requests/IRequestHandler.cs` |
| `Command` | Metadata for a command (name, access level, argument schema) | `src/Perpetuum/Command.cs` |
| `Commands` | Static registry of all ~200+ commands | `src/Perpetuum/Commands.cs` |
| `Zone` | Self-contained world simulation (process loop) | `src/Perpetuum/Zones/Zone.cs` |
| `ZoneManager` | Lookup/iterate across all running zones | `src/Perpetuum/Zones/ZoneManager.cs` |
| `ZoneSession` | Per-player connection within a zone | `src/Perpetuum/Zones/ZoneSession.cs` |
| `Entity` | Base game object with Eid and property bag | `src/Perpetuum/EntityFramework/Entity.cs` |
| `EntityDefault` | Static template/definition data (DB-loaded) | `src/Perpetuum/EntityFramework/EntityDefault.cs` |
| `Unit` | Entity with in-zone physics: armor, core, position, locking | `src/Perpetuum/Units/Unit.cs` |
| `Robot` | Unit that can equip modules; manages overheat | `src/Perpetuum/Robots/Robot.cs` |
| `Player` | Robot controlled by a connected character | `src/Perpetuum/Players/Player.cs` |
| `Module` | Equipment slot on a robot component | `src/Perpetuum/Modules/Module.cs` |
| `ActiveModule` | Module with a state machine (Idle/Oneshot/AutoRepeat/…) | `src/Perpetuum/Modules/ActiveModule.States.cs` |
| `ProcessManager` | Ticks all registered `IProcess` instances at ~50 ms | `src/Perpetuum/Threading/Process/ProcessManager.cs` |
| `HostStateService` | Tracks server lifecycle state (Off/Init/Starting/Online/Stopping) | `src/Perpetuum/Host/HostStateService.cs` |
| `Db` | Static fluent query factory backed by SQL Server | `src/Perpetuum/Data/Db.cs` |

## Sparky Local Answer Service

`Perpetuum.ItemStatsExporter` builds a versioned multilingual knowledge bundle on the Main development machine from the local database copy, validated custom dictionaries, and the reviewed knowledge-source catalog. Only that bundle is transferred to the AI PC. The AI-machine service reads the bundle, exposes an authenticated HTTPS API on IPv4 loopback through the reverse SSH forward, and uses the configured LM Studio model for language/intent interpretation and bounded, evidence-only phrasing. It has no database or source-repository access. Mechanics passages are curated player-facing summaries whose revision hashes include their supporting server files. Eleven explicitly allowlisted lore files are read from `E:\Perpetuum-lore`; their absolute paths are not stored in the bundle.

The service also starts a second HTTPS Kestrel listener on AI loopback port 5081 for the admin panel; the game API remains isolated on port 5080. Both listeners reuse the loopback TLS certificate, but the panel requires its own DPAPI-protected bearer token and rejects Dev/Live game API credentials. The bundled static UI manages separate Dev/Live profile revisions and immutable bundle selections, validates and activates changes without a service restart, and requires explicit promotion before a Dev revision affects Live. An optional AI-initiated SSH reverse tunnel maps the panel to Main loopback port 5082 with a separate private key and host-key pin, while the game tunnel keeps its existing destination port and credential. The panel listener never binds to a LAN or public address.

AI keeps request metadata, profile revisions, feedback, reviewed evaluation examples, and replay results in a local SQLite store. Replay runs an active revision and one explicitly selected draft through the bounded interpreter/answer path, then persists side-by-side outputs without a game or Discord delivery dependency. Admin exports are bounded and omit question/answer content by default. Independent retention clears captured question/answer/search/entity text, review notes, and replay output under content retention, then removes linked metadata under metadata retention. When the SQLite store is unavailable, answering can continue in the configured fallback mode, but admin profile mutation and replay are disabled so volatile changes are not presented as durable.

Robot knowledge is constructed on Main from each definition's real `robottemplaterelation`: the exporter creates the requested entity through the server factory, assembles its parts, and initializes it before reading combined and per-component stats. Chassis extension contributions are grouped and calculated separately at level 1, leaving baseline stats independent of character extensions. Module compatibility records use server slot and allowed-robot rules plus the unique-category restriction. They describe static eligibility only; each compatibility response appends a localized note that player loadout occupancy and total CPU/power-grid capacity were not checked. Localized views omit robots with no translated name and omit individual components, fields, units, or bonuses without public dictionary labels. Missing or invalid template relations are skipped instead of using fallback template facts.

The game server keeps the integration disabled by default. When enabled, natural in-game direct addresses and actual Discord bot mentions are admitted to a bounded request queue; tracked workers send only the question and optional public item/language hints to the AI PC. Caller IDs, channel routing, session state, and delivery decisions stay in the game-server process. The same source-platform delivery rules still apply: in-game chat for online callers, character mail for offline callers, and a Discord reply in the originating channel.

HTTP completions return as `SparkyResponse` events through `EventListenerService`. The game event processor posts game answers without the normal Discord mirror, sends offline answers through character mail, and routes Discord answers only to their source channel with mentions disabled. Short-lived follow-up context stores only a public item or robot name and response language in the existing volatile, source-separated store. The AI API does not retain conversation context. Bundle refresh validates and stages an immutable copy, then the panel activates it through a profile revision without restarting the game server. Game and admin bearer tokens are DPAPI-protected in the AI service settings; the VDM-side game bearer token is separately DPAPI-protected in an ACL-restricted file and is not stored in `perpetuum.ini`. Service, panel, bundle, and refresh instructions are documented in `src/Perpetuum.ItemStatsExporter/README.md`, `docs/superpowers/sparky-knowledge-refresh.md`, and `docs/superpowers/sparky-vdm-reverse-ssh-operations.md`; client setup is in `docs/superpowers/sparky-vdm-client-setup.md`.

## Sparky Passive Question Capture

The local-only question collector is a singleton used by `ChannelManager.Talk` and `EventListenerService.OnMessageReceived`. It is independently controlled by `SparkyQuestionCaptureEnabled` (default `false`), so the local development server can collect likely questions while `EnableSparky` remains off. It accepts only the configured four Discord channel IDs; the game path checks the linked channel ID and writes the `Game` record before mirroring, while the Discord path excludes bot-authored mirror messages and writes `Discord` records.

The deterministic candidate filter recognizes question marks and common question openings in English, German, Portuguese, and Russian. Every accepted record is synchronously appended and disk-flushed as one JSONL line. The archive defaults to `<GameRoot>\logs\sparky-question-capture.jsonl`; `<archive path>.session.json` preserves the original UTC deadline, and restarts do not extend it. Duration is configurable from 1 to 48 hours. The record excludes author and platform/account/channel/message IDs, redacts raw Discord user mentions, and retains message text (which may itself contain names). The service does not call the answer API or any external model. Setup and safe local pilot steps are in `docs/superpowers/sparky-question-capture.md`; do not enable the pilot on VDI.

## Key Patterns

### Dependency Injection (Autofac)
All services, handlers, and runtime objects are wired in `src/Perpetuum.Bootstrapper/`.
Each major subsystem has its own Autofac `Module` in `src/Perpetuum.Bootstrapper/Modules/`.
Constructor injection is the exclusive DI style.
Factory delegates (`Func<T>`, typed delegate aliases) are registered to allow deferred or
parameterised construction without breaking DI.

Startup order in `PerpetuumBootstrapper.Init()`:
1. `ContainerBuilder` assembles all modules
2. `IContainer` built; static singletons wired (`EntityDefault.Reader`, `Entity.Services`, …)
3. `InitGame()` calls `initServer` stored procedure to clean runtime DB tables
4. `HostStateService.State = Init → Starting → Online`

### Command / Request Handler Pattern

```
Client TCP frame
  → Session.OnDataReceived (GenxyReader deserialises key-value packet)
  → commandFactory(text) → Command lookup (keyed in Autofac by UPPER text)
  → RequestHandlerFactory<IRequest>(command) → IRequestHandler<IRequest>
  → handler.HandleRequest(request)
```

For zone-scoped commands (player is in a zone):
```
  → RequestHandlerFactory<IZoneRequest>(command) → IRequestHandler<IZoneRequest>
  → handler.HandleRequest(zoneRequest)   // zoneRequest.Zone is populated
```

All handlers live in `src/Perpetuum.RequestHandlers/` (597 .cs files across subdirectories).
Handlers are registered by `RequestHandlersModule` and `ZoneRequestHandlersModule` in the bootstrapper.
To add a command: define it in `src/Perpetuum/Commands.cs`, create a handler class,
register it in the appropriate bootstrapper module.

### Entity System

Inheritance chain (bottom-up):
```
Entity (Eid, DynamicProperties, Owner, Parent hierarchy)
  └─ Item (ItemProperty collection, volume, quantity)
       └─ Unit (position, armor, core, speed, locking, damage, effects)
            └─ Robot (module components, overheat, locking handler)
                 ├─ Player (character reference, mission handler, blob)
                 ├─ Npc (AI, flock membership)
                 └─ SmartCreature (CrystalAI behaviour)
```

`EntityDefault` holds **static definition data** loaded from the DB at startup.
`Entity.DynamicProperties` holds **mutable runtime state** as a key-value bag.
`OptionalProperty<T>` / `UnitOptionalProperty<T>` provide typed access to named properties
on units (`src/Perpetuum/Units/UnitOptionalProperty.cs`).
`ItemProperty` holds aggregate-field values with modifier chains on items/modules.

### Zone as a Process

`Zone` extends `Threading.Process.Process` (`src/Perpetuum/Threading/Process/Process.cs`).
`Zone.Update(TimeSpan)` is called by `ProcessManager` every ~50 ms.
Each zone holds:
- `ImmutableDictionary<long, Unit>` (lock-free updates via `ImmutableInterlocked`)
- `ImmutableDictionary<long, Player>`
- `ImmutableHashSet<ZoneSession>` (connected clients)
- Its own `TcpListener` on a dedicated port
- Terrain grid (`ITerrain`) with layered data (altitude, control, blocking, materials)
- NPC `PresenceManager` (flocks, presences, spawn rules)
- Zone-local services: beams, weather, decors, environment effects, plant handler

Zone types: `PveZone`, `PvpZone`, `StrongHoldZone`, `TrainingZone`
(`src/Perpetuum/Zones/PveZone.cs`, `PvpZone.cs`, `StrongHoldZone.cs`, `TrainingZone.cs`)

Zone configurations read from DB at startup via `ZoneConfigurationReader`
(`src/Perpetuum/Zones/ZoneConfiguration.cs`).

### Module State Machine

`ActiveModule` uses a `StackFSM` (stack-based finite state machine,
`src/Perpetuum/StateMachines/StackFSM.cs`).

States: `Idle`, `Oneshot`, `AutoRepeat`, `Disabled`, `AmmoLoad`, `Shutdown`

Module hierarchy:
```
Module (powergrid/CPU usage, property modifiers)
  └─ ActiveModule (state machine, ammo, cycling)
       ├─ WeaponModule (damage, range)
       ├─ ArmorRepairModule
       ├─ HarvesterModule / DrillerModule
       ├─ SensorJammerModule
       └─ … (40+ concrete types in src/Perpetuum/Modules/)
```

### Network & Wire Format

- TCP connections use RSA key exchange then RC4 stream cipher
  (`src/Perpetuum/Network/EncryptedTcpConnection.cs`)
- Wire format is **GenXY** — a custom text-based key=value protocol
  (`src/Perpetuum/GenXY/GenxyReader.cs`, `GenxyWriter.cs`)
- `GenxyConverter` allows registering custom type serialisers
- `MessageBuilder` / `IMessage` wrap outgoing packets
  (`src/Perpetuum/MessageBuilder.cs`)

### Data Access

`Db.Query()` returns a fluent `DbQuery` builder backed by `SqlConnection`.
All DB calls go through `System.Transactions.TransactionScope` (ReadCommitted).
`Transaction.Current.OnCommited(...)` is used extensively for post-commit side effects
(e.g., firing domain events after a successful DB write).
No ORM; raw SQL or stored procedure calls only.

## Domain Model

```
Account ──< Character ──< Session ──> Zone
                   │
                   └──> Robot (Player) ──< RobotComponent ──< Module
                                      └─ LockHandler ──< Lock
                                      └─ EffectHandler ──< Effect

Zone ──< Unit (Npc, PBS objects, gates, relics, eggs …)
     ──> ITerrain (AltitudeLayer, ControlLayer, BlockingLayer, Materials)
     ──> PresenceManager ──< Presence ──< Flock ──< Npc

Corporation ──< Character
Gang        ──< Character (transient, in-session)
```

## Data Flow

### Sign-in and Character Select

1. Client connects → `Session` created (`src/Perpetuum/Services/Sessions/Session.cs`)
2. RSA key exchange, RC4 established
3. `signIn` command → `SignIn` handler → authenticates account, sets `AccessLevel`
4. `characterSelect` command → `CharacterSelect` handler → `session.SelectCharacter(character)`
5. Client sends `zoneEnter` → zone's `TcpListener` receives new connection
6. `ZoneSession` created for that zone; player's `Robot` spawned via `ZoneEnterQueueService`

### In-Zone Request

1. Client sends GenXY frame on the zone socket
2. `ZoneSession.OnDataReceived` deserialises packet
3. `RequestHandlerFactory<IZoneRequest>` resolves handler by command text
4. Handler executes, may mutate unit state or DB, sends reply via `session.SendMessage`

### Zone Tick

1. `ProcessManager` fires `zone.Update(elapsed)` every ~50 ms
2. Zone updates sessions, units, NPC presences, terrain change notifications
3. `Unit.Update` runs module states, locks, movement, effect ticks
4. Changed unit properties trigger update packets broadcast to nearby players

## Concurrency Model

- **Single process manager thread** ticks all registered `IProcess` instances at ~50 ms intervals
  (`src/Perpetuum/Threading/Process/ProcessManager.cs`).
- **Each zone runs as an `IProcess`** in that single process loop (no per-zone dedicated thread).
  Zone state mutation (units/sessions dictionaries) uses `ImmutableInterlocked` for lock-free
  reads from other threads.
- **Network I/O** is async (socket `BeginReceive`/`BeginSend`); callbacks marshal into the
  process manager's execution context.
- **Database transactions** use `System.Transactions.TransactionScope` (ReadCommitted).
  `Transaction.Current.OnCommited(...)` extension method defers side effects until commit.
- **No Task/async-await** in core game logic; async is isolated to network receive loops and
  the `AsyncProcess` wrapper (`src/Perpetuum/Threading/Process/AsyncProcess.cs`).

## Key Subsystems

**Zones** (`src/Perpetuum/Zones/`)
86 subdirectories covering: NPC system (`NpcSystem/`), player-built structures (`PBS/`),
intrusion mechanics (`Intrusion/`), terrain (`Terrains/`), combat effects (`Effects/`),
locking (`Locking/`), teleporting (`Teleporting/`), scanning, mining, harvesting, gates,
rifts, relics, blobs, proximity devices, training.

**NPC System** (`src/Perpetuum/Zones/NpcSystem/`)
AI behaviour via CrystalAI (`CrystalAI/`) and custom AI (`AI/`). NPCs organised into
`Flocks` within `Presences`. Multiple presence types: static, expiring, growing,
interzone, random. Reinforcement and SAP attacker subsystems.

**PBS (Player-Built Structures)** (`src/Perpetuum/Zones/PBS/`)
Docking bases, control towers, turrets, reactors, energy wells, production nodes,
armor repairers, highway nodes, effect nodes — all as `PBSObject` entities.

**Market Engine** (`src/Perpetuum/Services/MarketEngine/`)
Order book (`MarketOrder`), price collection, auto-orders, robot price writer,
cleanup service. Initialised after zones load (`MarketHelper.Init()`).

**Mission Engine** (`src/Perpetuum/Services/MissionEngine/`)
Mission definitions, spots, in-progress tracking, targets, rewards, bonus objects,
transport assignments. Data cached at startup via `MissionDataCache`.

**Production Engine** (`src/Perpetuum/Services/ProductionEngine/`)
Crafting, research, calibration, reprocessing. `ProductionManager` ticks active lines.

**Seasons** (`src/Perpetuum/Services/Seasons/`)
Season lifecycle (active/inactive), activity bonuses, repository, cache.

**Event Services** (`src/Perpetuum/Services/EventServices/`)
Pub/sub event bus (`EventListenerService`). NPC spawn events, environmental effects,
EP bonus events dispatched through it.

**Standing** (`src/Perpetuum/Services/Standing/`)
Faction and corporation standing lookup, cached, updated on mission completion/combat.

**Sessions** (`src/Perpetuum/Services/Sessions/`)
`SessionManager` — tracks all active `ISession` instances; exposes character-deselect events
consumed by zones to clean up zone sessions.

## Architectural Constraints

- **Threading:** Single `ProcessManager` loop at ~50 ms. Zone logic runs in that loop.
  Network callbacks are async and lock-free (immutable collections + `ImmutableInterlocked`).
- **Global state:** Several static service locators set during bootstrap:
  `Entity.Services` (`EntityFramework/Entity.cs`), `EntityDefault.Reader`, `Db.DbQueryFactory`,
  `Character.CharacterFactory`, `MissionHelper`, `PBSHelper`, `Message.MessageBuilderFactory`.
  These are write-once at startup, read-only thereafter.
- **Platform:** `[SupportedOSPlatform("windows")]` on both `Perpetuum.Server` and
  `Perpetuum.Bootstrapper` assemblies — Windows-only due to native dependencies.
- **Partial test coverage:** `src/Perpetuum.Tests` (unit) and `src/Perpetuum.Tests.Integration`
  (against the real database) cover the data layer, validation helpers and two regression paths.
  Gameplay behaviour is still validated manually or via the `Perpetuum.AdminTool` WPF application.
  See `docs/codebase/TESTING.md`.
- **SQL Server only:** `Microsoft.Data.SqlClient` is hard-wired; no abstraction layer.

## Anti-Patterns

### Static Service Locators

**What happens:** `Entity.Services`, `Db.DbQueryFactory`, `MissionHelper`, `PBSHelper`,
`Character.CharacterFactory`, and several others are static properties set during
`PerpetuumBootstrapper.Init()`.
**Why it's wrong:** Makes unit testing impossible and hides dependencies.
**Do this instead:** Inject the dependency via constructor. This is the pattern used in
all newer code (e.g., `SeasonService`, zone-scoped services).

### Property-Injection on Zone

**What happens:** `Zone` properties like `Terrain`, `Beams`, `Weather`, `PresenceManager`
are public setters wired by Autofac property injection rather than constructor injection.
**Why it's wrong:** Violates explicit dependencies; object is partially constructed
between `new` and full property assignment.
**Do this instead:** Consolidate into constructor parameters or a typed config struct,
as done in newer subsystems.

## Error Handling

**Strategy:** Synchronous exception propagation with typed `ErrorCodes` enum.

**Patterns:**
- `.ThrowIfNull(ErrorCodes.X)` extension on nullable results — throw `PerpetuumException`
- `.ThrowIfZero(ErrorCodes.X)` on `ExecuteNonQuery` results to detect missing rows
- `PerpetuumException` carries an `ErrorCodes` value sent back to the client as a
  structured error message (`Message.Builder.WithError(error)`)
- Unhandled exceptions in process loops are logged via `Logger.Exception(ex)` without
  crashing the host

## Cross-Cutting Concerns

**Logging:** `Logger` static facade (`src/Perpetuum/Log/`) with pluggable `ILogger<T>` sink.
Chat events use a separate typed `ILogger<ChatLogEvent>`.
**Validation:** Argument schema declared on `Command.Arguments` list; checked via
`command.CheckArguments(data)` before handler dispatch.
**Authentication:** `AccessLevel` enum checked per command. `Session.AccessLevel` is set
on sign-in and elevated for admin accounts.
**Transactions:** `Db.CreateTransaction()` wraps all DB mutations in `TransactionScope`.
Post-commit hooks via `Transaction.Current.OnCommited(...)` (`src/Perpetuum/Data/TransactionExtensions.cs`).

---

*Architecture analysis: 2026-05-11*

## Graph Artifact

A structural graph of this codebase is generated by graphify-dotnet on every `Perpetuum.Server`
build (requires .NET 10 SDK and `dotnet tool restore`):

- `docs/graph/graph.json` — machine-readable nodes/edges (gitignored, regenerates on build)
- `docs/graph/GRAPH_REPORT.md` — Markdown architecture report (gitignored, regenerates on build)
- GitHub Wiki — latest report published by CI on each push to `develop`:
  `https://github.com/OpenPerpetuum/PerpetuumServer2/wiki/Codebase-Graph`

See `.claude/knowledge/codebase-graph.md` for how Claude uses these artifacts.
