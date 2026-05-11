# Coding Conventions

**Analysis Date:** 2026-05-11

## Naming Conventions

**Classes:**
- PascalCase throughout: `SeasonService`, `MarketHandler`, `ZoneSession`, `RequestHandlerProfiler<T>`
- Interfaces prefixed with `I`: `IRequestHandler`, `IZone`, `ISeasonService`, `IProcess`, `IReadOnlyRepository<TId,T>`
- Abstract base classes without prefix: `Process`, `Entity`, `Module`
- Handler classes named `<Noun><Verb>` or `<Domain><Action>`: `MarketHandler`, `ChangeAmmo`, `SignIn`, `AccountList`
- Repository classes named `<Domain>Repository`: `SeasonRepository`, `MarketOrderRepository`, `CharacterProfileRepository`
- Service classes named `<Domain>Service`: `SeasonService`, `MarketCleanUpService`, `EPBonusEventService`
- Exception classes suffixed `Exception`: `PerpetuumException`
- Enum types in PascalCase: `ErrorCodes`, `ModuleStateType`, `SeasonActivityType`

**Methods:**
- PascalCase public methods: `HandleRequest`, `RefreshCache`, `AddPoints`, `GetActiveSeason`
- PascalCase private methods: `LoadTerminalPositions`, `FlushLogInfos`, `OnSessionAdded`
- Event handler methods prefixed `On`: `OnStateChanged`, `OnSessionAdded`, `OnCharacterLogin`, `OnDynamicPropertiesUpdated`
- Factory methods named `Create` or `Get`: `PerpetuumException.Create(error)`, `Db.Query()`
- Boolean-returning methods use `Is`, `Has`, `Can`, `Try` prefixes: `IsSellable`, `IsAmmoable`, `TryGet`, `TryMarkIntroMailSent`

**Fields:**
- Private fields prefixed with underscore, camelCase: `_repository`, `_sessionManager`, `_activeSeason`
- Public static readonly fields (constants/singletons) use PascalCase: `EntityDefault.None`, `ZoneSession.None`
- Static fields for factories/services use PascalCase: `Entity.Services`, `EntityDefault.Reader`
- Enum-like static fields on classes use PascalCase: `ResolveTestTaskCreationOptions`

**Properties:**
- PascalCase public auto-properties: `Id`, `Name`, `IsActive`, `Configuration`
- Backing fields use underscore prefix: `_owner` backing `Owner`, `_health` backing `Health`

**Parameters:**
- camelCase: `characterId`, `seasonId`, `objectiveId`, `commandText`

**Type Parameters:**
- Single-letter `T`, or descriptive `TId`, `TKey`, `TValue`

**Keys / Constants:**
- String keys for request data use the `k.*` constant pattern (e.g. `k.characterID`, `k.robotEID`, `k.zone`). Defined in a static class `k` in `src/Perpetuum/`

## Code Organization

**File naming:**
- One public type per file, file name matches type name
- Partial classes split across multiple files with suffix after dot: `Robot.cs`, `Robot.Helpers.cs`, `Robot.Locking.cs`, `Robot.Properties.cs`; also `ActiveModule.cs`, `ActiveModule.States.cs`, `ActiveModule.Ammo.cs`
- Interface files named `I<Type>.cs`: `IZone.cs`, `IProcess.cs`, `ISeasonService.cs`

**Namespace structure:**
- Root namespace: `Perpetuum`
- Subsystem namespaces: `Perpetuum.Services.Seasons`, `Perpetuum.Zones.NpcSystem`, `Perpetuum.RequestHandlers.Markets`
- Mirrors directory structure under `src/Perpetuum/`

**Class organization within files:**
- Section dividers use `// ── Section Name ──` comment style (seen in `SeasonRepository.cs`, `SeasonService.cs`)
- Static singletons/factories defined at top of class
- Constructor follows fields
- Public API before private helpers

**Static service locators:**
- `Entity.Services` (static injection point), `EntityDefault.Reader`, `Db.DbQueryFactory`, `Logger.Current` — these are static properties set during bootstrapping rather than constructor injection in some core classes

## Patterns in Use

**Command/Handler pattern:**
- Each client command maps 1:1 to a handler class implementing `IRequestHandler` (or `IRequestHandler<IZoneRequest>` for zone-scoped commands)
- Handlers registered via Autofac in `src/Perpetuum.Bootstrapper/Modules/RequestHandlersModule.cs` and `ZoneRequestHandlersModule.cs`
- Handler `HandleRequest(IRequest request)` is the single entry point; data extracted via `request.Data.GetOrDefault<T>(k.key)`

**Repository pattern:**
- `IRepository<TId, T>` / `IReadOnlyRepository<TId, T>` interfaces
- Concrete implementations query via `Db.Query()` fluent API
- `CachedReadOnlyRepository<TId,T>` wraps any repository with `ObjectCache`-backed caching (`src/Perpetuum/CachedReadOnlyRepository.cs`)

**Process/game-loop pattern:**
- Long-running services extend `Process` (`src/Perpetuum/Threading/Process/Process.cs`) and override `Update(TimeSpan time)`
- `ProcessManager` owns a dedicated `Thread` ("MainLoop") that calls `Update` on all registered processes on a fixed interval
- Zone processes and services register with `IProcessManager` via Autofac

**State machine pattern:**
- `StackFSM` (stack-based FSM) used for module states in `src/Perpetuum/StateMachines/`
- States implement `IState`; `IModuleState` wraps FSM states for module behavior
- Module state types enumerated in `ModuleStateType` enum

**Builder pattern:**
- `IBuilder<T>` with `Build()` method in `src/Perpetuum/Builders/IBuilder.cs`
- `Message.Builder.FromRequest(request).WithData(d).WrapToResult().Send()` — fluent builder for wire responses

**Guard / fluent-throw pattern:**
- `src/Perpetuum/Guard.cs` provides extension methods for inline validation:
  - `value.ThrowIfNull(ErrorCodes.X)`
  - `value.ThrowIfFalse(ErrorCodes.X)`
  - `value.ThrowIfTrue(ErrorCodes.X)`
  - `value.ThrowIfEqual(comparand, ErrorCodes.X)`
  - `value.ThrowIfNotEqual(comparand, ErrorCodes.X)`
  - `value.ThrowIfLess(comparand, ErrorCodes.X)` / `ThrowIfGreater`
  - `value.ThrowIfNotType<T>(ErrorCodes.X)`
  - `errorCode.ThrowIfError()`
- These are the **standard** way to validate preconditions in handlers and services

**Observer / event pattern:**
- `IObservable<T>` / `IObserver<T>` used for event messages (`IEventProcessor : IObserver<IEventMessage>`)
- C# events used for session lifecycle: `SessionAdded`, `CharacterSelected`

**Dependency injection:**
- Autofac; all services injected via constructor
- Autofac modules in `src/Perpetuum.Bootstrapper/Modules/` — one module per subsystem
- Some core singletons use static service locator during startup (`Entity.Services`, `Logger.Current`) for legacy reasons

**Immutable state for thread safety:**
- `ImmutableList<T>` with `ImmutableInterlocked.Update` for `ProcessManager._processes`
- `volatile` fields for frequently-read single values: `_activeSeason`, `_lastNotifiedSeasonId`
- `ImmutableHashSet<Entity>` for entity children collection

## Error Handling

**Domain errors:**
- `PerpetuumException(ErrorCodes error)` is the standard domain exception
- `ErrorCodes` enum in `src/Perpetuum/ErrorCodes.cs` enumerates all possible game-logic error conditions
- Throw via `PerpetuumException.Create(error)` or `throw new PerpetuumException(ErrorCodes.X)`
- Contextual data attached via `.SetData(key, value)` chaining on `PerpetuumException`
- Common pattern: `someValue.ThrowIfNull(ErrorCodes.ItemNotFound)` — see Guard pattern above

**System/infrastructure errors:**
- `Logger.Exception(ex)` logs exceptions without rethrowing
- `Task.LogExceptions()` extension on `TaskExtensions` (`src/Perpetuum/TaskExtensions.cs`) attaches a fault-continuation that logs `AggregateException` inner exceptions
- Fire-and-forget tasks use `.LogExceptions()`: `Task.Run(OnDisconnected).ContinueWith(t => Dispose()).LogExceptions()`
- `try/catch (PerpetuumException gex)` at handler call sites to send error responses to client

**Transaction handling:**
- Database mutations wrapped in `using (var scope = Db.CreateTransaction()) { ... scope.Complete(); }`
- `Transaction.Current.OnCompleted(completed => { ... })` used to send client responses only after DB commit succeeds

## Async Patterns

**Primary threading model:**
- Not async/await-based. The codebase uses `Task.Run`, `Task.Factory.StartNew`, `Thread`, and `ThreadPool` directly
- `async`/`await` appears only in `EventListenerService.cs` for the Discord.NET client (external SDK requirement)
- No `ConfigureAwait` usage — async is not the dominant pattern

**Fire-and-forget:**
- `Task.Run(() => ...).LogExceptions()` — standard pattern for background work that should not block the caller
- `Task.Run(...).ContinueWith(t => ...)` — continuation chaining for post-async actions

**Parallel work:**
- `Task.Factory.StartNew(..., ResolveTestTaskCreationOptions, TaskScheduler.Default)` for CPU-bound parallel batch work (mission resolution testing)
- `Task.WaitAll(tasks.ToArray())` for joining parallel batches

**ThreadPool direct:**
- `ThreadPool.UnsafeQueueUserWorkItem(_ => ..., null)` used in `MessageSender` and `TcpConnection` for low-latency network dispatch

**Game loop:**
- `ProcessManager` runs a named background thread ("MainLoop") that tick-calls all `IProcess.Update(TimeSpan)` — this is the primary game simulation clock

## Comments & Documentation

**XML doc comments:**
- Used selectively on public APIs, especially utilities and domain types
- `<summary>`, `<param>`, `<returns>` tags present on ~250 files
- Not exhaustive — many handler classes have no XML docs

**Inline comments:**
- Section dividers in long files: `// ── Section Name ────────────────────────────────────────────────────`
- Clarifying comments on non-obvious logic: `//target module is empty`, `//clean pbshighway bit`
- `//... other conditions` placeholder-style comments exist (low-quality legacy areas)

**JetBrains annotation attributes:**
- `[NotNull]`, `[CanBeNull]` from a local copy of JetBrains annotations in `src/Perpetuum/Annotations.cs`
- `[UsedImplicitly]`, `[Pure]`, `[InstantHandle]` used throughout
- These are compile-time documentation and static analysis hints only — no runtime enforcement

**`DEPRECATED_` prefix convention:**
- Enum members in `ErrorCodes` that are obsolete are prefixed `DEPRECATED_` rather than removed, to preserve numeric values

---

*Convention analysis: 2026-05-11*
