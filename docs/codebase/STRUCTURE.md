# Project Structure

**Analysis Date:** 2026-05-11

## Solution Layout

```
PerpetuumServer2.sln
├── src/Perpetuum/                      # Core library — all game logic
├── src/Perpetuum.Bootstrapper/         # Autofac DI wiring
├── src/Perpetuum.RequestHandlers/      # 150+ command handler classes
├── src/Perpetuum.ExportedTypes/        # Shared enum/type definitions
├── src/Perpetuum.Server/               # Console entry point
├── src/Perpetuum.ServerService2/       # Windows service wrapper
├── src/Perpetuum.ServerService2Installer/ # Windows service installer
├── src/Perpetuum.AdminTool/            # WPF admin/tooling application
├── src/Open.Nat/                       # UPnP library (vendored)
├── docs/                               # DB schema docs (authoritative)
│   └── db_structure/
│       ├── database_schema_documentation.md
│       ├── stored_procedures/
│       ├── functions/
│       ├── views/
│       └── data_types/
└── packages/                           # NuGet package cache
```

## Project Roles

| Project | Role |
|---------|------|
| `Perpetuum` | All game logic: entities, zones, modules, robots, players, services |
| `Perpetuum.Bootstrapper` | Autofac container assembly; one `Module` per subsystem |
| `Perpetuum.RequestHandlers` | Handler class per command; no game logic, thin orchestration |
| `Perpetuum.ExportedTypes` | Enums/flags shared across projects (`AggregateField`, `CategoryFlags`, `EffectType`, etc.) |
| `Perpetuum.Server` | `Program.cs` — parses CLI args, calls `PerpetuumBootstrapper.Init/Start` |
| `Perpetuum.ServerService2` | Windows service host wrapping bootstrapper |
| `Perpetuum.AdminTool` | Standalone WPF tool for admin operations (NPC editing, seasons, loot, etc.) |

## Source Organisation — `src/Perpetuum/`

```
src/Perpetuum/
├── EntityFramework/        # Entity, EntityDefault, DynamicProperty, EntityFactory, EntityRepository
├── Units/                  # Unit base class, UnitOptionalProperty, DockingBases, FieldTerminals
├── Robots/                 # Robot, RobotComponent, RobotHead/Leg/Chassis, RobotSetup, Fitting
├── Players/                # Player class
├── Modules/                # Module, ActiveModule (+States), 40+ concrete module types
│   ├── Weapons/
│   ├── EffectModules/
│   ├── Terraforming/
│   ├── RemoteControl/
│   ├── AdaptiveAlloy/
│   └── ModuleProperties/
├── Zones/                  # Zone, ZoneManager, ZoneSession, IZone, ZoneConfiguration
│   ├── NpcSystem/          # Npc, SmartCreature, AI, CrystalAI, Flocks, Presences, …
│   ├── PBS/                # Player-built structures (docking bases, turrets, towers, …)
│   ├── Terrains/           # ITerrain, layers (altitude/control/blocking), materials, plants
│   ├── Intrusion/          # SAP mechanics, outpost stability, intrusion events
│   ├── Locking/            # LockHandler, Lock types, LockState
│   ├── Effects/            # Zone effects, effect handler
│   ├── DamageProcessors/   # DamageProcessor, DamageTakenEventArgs
│   ├── Movements/          # Movement, PathMovement, RandomMovement, WaypointMovement
│   ├── Teleporting/        # Teleport strategies, spark teleport
│   ├── Scanning/           # Scanning modules, ammos, results
│   ├── Beams/              # Visual beam effects
│   ├── Decors/             # Decorative objects
│   ├── Environments/       # Environmental conditions
│   ├── Finders/            # Position finders, unit finders
│   ├── Helpers/            # Zone utility helpers
│   ├── Artifacts/
│   ├── Blobs/
│   ├── CombatLogs/
│   ├── Eggs/
│   ├── FieldEffectGenerators/
│   ├── Gates/
│   ├── LandMines/
│   ├── LootContainers/
│   ├── PlantTools/
│   ├── ProximityDevices/
│   ├── ProximityProbes/
│   ├── PunchBags/
│   ├── RemoteControl/
│   ├── TerraformProjects/
│   ├── Training/
│   ├── Visibility/
│   └── ZoneEntityRepositories/
├── Services/
│   ├── Sessions/           # ISession, Session, SessionManager
│   ├── MarketEngine/       # Market orders, pricing, auto-orders
│   ├── MissionEngine/      # Missions, targets, transport assignments, data cache
│   ├── ProductionEngine/   # Crafting, research, calibration, reprocessing
│   ├── Seasons/            # Season lifecycle and bonus system
│   ├── EventServices/      # Pub/sub event bus, NPC spawn events
│   ├── Standing/           # Faction/corp standing
│   ├── Social/             # Friends, blocks, social list
│   ├── Channels/           # Chat channels, chat commands
│   ├── ExtensionService/   # Character skill extensions
│   ├── Insurance/          # Robot insurance
│   ├── ItemShop/           # In-game item shop
│   ├── Looting/            # Loot tables, loot service
│   ├── Mail/               # In-game mail
│   ├── HighScores/         # Leaderboard tracking
│   ├── Relics/             # Relic spawning and management
│   ├── RiftSystem/         # Rift spawning
│   ├── Sparks/             # Spark teleport system
│   ├── Strongholds/        # Stronghold zone player state
│   ├── TechTree/           # Tech tree progression
│   ├── Trading/            # Player-to-player trade
│   ├── Steam/              # Steam authentication
│   ├── GameTime/           # Server time utilities
│   ├── Relay/              # Relay server info
│   ├── Artifacts/
│   └── Weather/
├── GenXY/                  # Wire protocol: GenxyReader, GenxyWriter, GenxyConverter
├── Network/                # TCP connections, encryption (RSA/RC4)
├── Host/                   # HostStateService, HostState, IRequestHandler, RequestHandlerFactory
├── Data/                   # Db static factory, DbQuery fluent builder, TransactionExtensions
├── StateMachines/          # StackFSM, FiniteStateMachine, IState
├── Threading/              # Process, ProcessManager, AsyncProcess, ImmutableInterlocked helpers
├── Timers/                 # IntervalTimer, various timer types
├── Collections/            # Spatial collections
├── Groups/                 # Corporations, Gangs, Alliances
├── Accounting/             # Account, Character, wallet, transaction logging
├── Containers/             # Container entities, system containers
├── Deployers/              # Item/entity deployers
├── Common/                 # Loggers, miscellaneous utilities
├── IO/                     # File system abstraction
├── Log/                    # Logger facade, ILogger<T>
├── Wallets/                # Wallet abstraction
└── Commands.cs             # Static registry of all ~200 commands
```

## Source Organisation — `src/Perpetuum.Bootstrapper/`

```
src/Perpetuum.Bootstrapper/
├── PerpetuumBootstrapper.cs    # Top-level init/start/stop; wires all modules
├── Modules/
│   ├── AutoActivatedTypesModule.cs
│   ├── ChannelTypesModule.cs
│   ├── CommandsModule.cs           # Registers all Commands as keyed instances
│   ├── EffectsModule.cs
│   ├── EntitiesModule.cs           # Entity system, all entity types, factories
│   ├── IntrusionsModule.cs
│   ├── LoggersModule.cs
│   ├── MissionsModule.cs
│   ├── MtProductsModule.cs
│   ├── NpcsModule.cs               # NPC flocks, presences, reinforcements, SAP
│   ├── PbsModule.cs
│   ├── RelicsModule.cs
│   ├── RequestHandlersModule.cs    # Maps every IRequest command to a handler class
│   ├── RiftsModule.cs
│   ├── RobotTemplatesModule.cs
│   ├── SeasonModule.cs
│   ├── TerrainsModule.cs
│   ├── ZoneRequestHandlersModule.cs # Maps every IZoneRequest command to a handler class
│   └── ZonesModule.cs              # Zone types, zone services, session factory
├── ContainerBuilderExtensions.cs
├── EntityAggregateServices.cs
├── RobotTemplateServices.cs
└── TeleportStrategyFactories.cs
```

## Source Organisation — `src/Perpetuum.RequestHandlers/`

```
src/Perpetuum.RequestHandlers/
├── AdminTools/
├── Characters/
├── Corporations/
│   └── YellowPages/
├── Extensions/
├── FittingPreset/
├── Gangs/
├── Intrusion/
├── Mails/
├── Markets/
├── Production/
├── RobotTemplates/
├── Socials/
├── Sparks/
├── Standings/
├── TechTree/
├── Trades/
├── TransportAssignments/
└── Zone/                  # Zone-scoped handlers (movement, combat, terrain, etc.)
```
597 `.cs` files total across all subdirectories.

## Key Files

**Entry Points:**
- `src/Perpetuum.Server/Program.cs` — console entry; parses `--GameRoot`, calls `bootstrapper.Init/Start`
- `src/Perpetuum.ServerService2/` — Windows service wrapper for the same bootstrapper

**Bootstrap:**
- `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs` — `Init(gameRoot)` builds the DI container,
  loads DB config, wires static service locators, initialises mission/market caches

**Command Registry:**
- `src/Perpetuum/Commands.cs` — every playable command defined as a `public static readonly Command`

**Zone Root:**
- `src/Perpetuum/Zones/Zone.cs` — abstract zone; subclassed by PveZone/PvpZone/StrongHoldZone/TrainingZone
- `src/Perpetuum/Zones/ZoneConfiguration.cs` — reads zone rows from DB; assigns listener ports

**Entity Root:**
- `src/Perpetuum/EntityFramework/Entity.cs`
- `src/Perpetuum/EntityFramework/EntityDefault.cs`

**Data Access:**
- `src/Perpetuum/Data/Db.cs` — `Db.Query()` fluent builder
- `src/Perpetuum/Data/TransactionExtensions.cs` — `OnCommited` hook

**Wire Protocol:**
- `src/Perpetuum/GenXY/GenxyReader.cs`
- `src/Perpetuum/GenXY/GenxyWriter.cs`
- `src/Perpetuum/GenXY/GenxyConverter.cs`

**Configuration:**
- `src/Perpetuum.ServerService2/appsettings.json` — sets `GameRoot` path
- `perpetuum.ini` (inside GameRoot) — SQL connection string, ports, zone config, feature flags

## Module Organisation (Autofac)

Modules registered in `PerpetuumBootstrapper.InitContainer()`:

| Module | Registers |
|--------|-----------|
| `CommandsModule` | All `Command` instances keyed by uppercase text |
| `RequestHandlersModule` | ~200 `IRequestHandler<IRequest>` mappings |
| `ZoneRequestHandlersModule` | Zone-scoped `IRequestHandler<IZoneRequest>` mappings |
| `EntitiesModule` | Entity system; all entity/item/module/robot/NPC concrete types |
| `ZonesModule` | Zone types, zone services (terrain, weather, beams, etc.), ZoneSession factory |
| `NpcsModule` | Flock configs, presence types, reinforcements, SAP attackers |
| `MissionsModule` | MissionDataCache, MissionProcessor, mission structures |
| `TerrainsModule` | Terrain layers, material readers, plant rules |
| `PbsModule` | PBS object types and handlers |
| `RelicsModule` | Relic manager and repository |
| `RiftsModule` | Rift manager and custom rift config |
| `IntrusionsModule` | SAP types, intrusion site handlers |
| `EffectsModule` | Effect types and handlers |
| `RobotTemplatesModule` | Robot template loading and relations |
| `SeasonModule` | SeasonService, SeasonRepository |
| `ChannelTypesModule` | Chat channel type registrations |
| `MtProductsModule` | Market/trade product registrations |
| `LoggersModule` | Logger implementations |
| `AutoActivatedTypesModule` | Types that must be instantiated at startup |

Additional registrations in `PerpetuumBootstrapper.InitContainer()` and `InitRelayManager()`
cover: Sessions, Corporations, Gangs, Market, Production, Standing, Social, Insurance,
LootService, TechTree, Steam, Sparks, Wallets, TradeService, and more.

## Naming Conventions

**Files:**
- One class per file; file name matches class name exactly
- Partial classes use `ClassName.PartName.cs` (e.g., `ActiveModule.States.cs`, `Zone.cs` + `ZoneExtensions.Gang.cs`)
- Interfaces prefixed with `I` (`IZone`, `IRequestHandler`, `ISession`)
- Handlers named `<CommandName>.cs` matching the command text (PascalCase)

**Directories:**
- Feature-named subdirectories within each project layer
- Zone sub-features grouped under `src/Perpetuum/Zones/<FeatureName>/`
- Services grouped under `src/Perpetuum/Services/<ServiceName>/`

## Where to Add New Code

**New game command:**
1. Define the `Command` in `src/Perpetuum/Commands.cs`
2. Create handler class in `src/Perpetuum.RequestHandlers/<Category>/` implementing
   `IRequestHandler` or `IRequestHandler<IZoneRequest>`
3. Register in `src/Perpetuum.Bootstrapper/Modules/RequestHandlersModule.cs` or
   `ZoneRequestHandlersModule.cs`

**New service:**
1. Implement in `src/Perpetuum/Services/<ServiceName>/`
2. Register in the appropriate Autofac module in `src/Perpetuum.Bootstrapper/Modules/`,
   or add registration directly in `PerpetuumBootstrapper.InitContainer()`
3. If the service needs ticking, add to `IProcessManager` via
   `.ToAsync().AsTimed(interval)` pattern in the `OnActivated` callback

**New zone feature:**
1. Add logic in `src/Perpetuum/Zones/<FeatureName>/`
2. Add the interface/property to `IZone` and implement in `Zone.cs` if it is
   zone-wide; otherwise inject via ZoneSession or unit

**New entity type:**
1. Subclass appropriate base (`Item`, `Unit`, `Robot`) in `src/Perpetuum/`
2. Register concrete type in `EntitiesModule.cs` via `builder.RegisterType<T>()`
3. Add `CategoryFlags` or `DefinitionNames` entry in `src/Perpetuum.ExportedTypes/`

**New module type:**
1. Subclass `Module` or `ActiveModule` in `src/Perpetuum/Modules/`
2. Register in `EntitiesModule.cs`

## Special Directories

**`docs/db_structure/`:**
- Authoritative source for all DB schema, stored procedures, functions, views
- Must be consulted before writing any SQL, query, or DB-touching code
- Not generated at build time; maintained manually

**`src/Perpetuum.AdminTool/`:**
- Standalone WPF application for server admin tasks
- Has its own data access layer under `src/Perpetuum.AdminTool/Data/`
- Does not share the bootstrapper; connects directly to the DB

**`bin/x64/Release/net8.0/`:**
- CI build output directory
- Generated, not committed

---

*Structure analysis: 2026-05-11*
