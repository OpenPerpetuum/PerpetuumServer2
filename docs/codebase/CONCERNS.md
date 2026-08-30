# Technical Concerns

**Analysis Date:** 2026-05-11

---

## Technical Debt

### Ambient Service Locator (Static Setters) — Widespread

Numerous classes expose services via `public static ... { get; set; }` properties that are injected by the Autofac bootstrapper at startup. This is the service-locator anti-pattern and is pervasive:

- `Db.DbQueryFactory` — `src/Perpetuum/Data/Db.cs:7`
- `Entity.Services` — `src/Perpetuum/EntityFramework/Entity.cs:10`
- `EntityDefault.Reader` — `src/Perpetuum/EntityFramework/EntityDefault.cs:10`
- `Character.CharacterCache`, `Character.CharacterFactory` — `src/Perpetuum/Accounting/Characters/Character.cs:94-96`
- `Logger.Current` — `src/Perpetuum/Log/Logger.cs:10`
- `MissionTarget.missionDataCache`, `MissionTarget.ProductionDataAccess` — `src/Perpetuum/Services/MissionEngine/MissionTargets/MissionTarget.cs:125-131`
- `MissionInProgress.MissionInProgressFactory`, `MissionInProgress.MissionProcessor` — `src/Perpetuum/Services/MissionEngine/Missions/MissionInProgress.cs:79-80`
- `PBSHelper.ProductionDataAccess`, `PBSHelper.ProductionManager` — `src/Perpetuum/Zones/PBS/PBSHelper.cs:37-38`
- `SeasonServiceLocator.Instance` — `src/Perpetuum/Services/Seasons/SeasonServiceLocator.cs:5`
- ~25 more static setters across the codebase

**Impact:** Makes dependency graphs invisible, causes test-unfriendly design, and risks null-reference panics if boot order shifts. The `SeasonServiceLocator` pattern was reintroduced for Seasons rather than injecting `ISeasonService` directly into call sites.

**Fix approach:** Replace with constructor injection where feasible. For cross-cutting concerns (e.g., `Db`, `Logger`), accept the cost but document clearly and avoid propagating the pattern to new code.

---

### Hardcoded Constants and Magic Values

- System character detection by nickname substring: `c.Nick.Contains("[OPP]")` — `src/Perpetuum/Accounting/Characters/Character.cs:76` — a TODO acknowledges this is fragile.
- Mission beta zone multiplier is a TODO: `//TODO: Beta multiplier!` — `src/Perpetuum/Services/MissionEngine/Missions/MissionVisitor.cs:72`
- Economy parameters hardcoded in C# rather than in DB: multiple TODOs: `src/Perpetuum/Services/MissionEngine/Missions/MissionInProgress.cs:1477`, `src/Perpetuum/Services/MissionEngine/MissionTargets/MissionTargetRewardCalculator.cs:70`, `src/Perpetuum/Services/MissionEngine/MissionTargets/RandomTargetObjects.cs:390`, `src/Perpetuum/Services/MissionEngine/Missions/RandomMission.cs:67`
- Mission Alpha zone assumed as neutral: `src/Perpetuum/Services/MissionEngine/Missions/MissionInProgress.cs:406` — marked `//TODO: Fixme`
- MissionLocation has a known "Syndicatification hack": `src/Perpetuum/Services/MissionEngine/MissionStructures/MissionLocation.cs:221`
- ZoneEffect player-only flag is hardcoded `true` pending a new DB column: `src/Perpetuum/Zones/Effects/ZoneEffects/ZoneEffectReader.cs:23`

**Impact:** Tuning economy or game balance requires C# changes and redeployment instead of DB updates.

**Fix approach:** Extract economy constants to DB configuration tables; expose them via admin commands.

---

### Excluded LootContainers Directory

The `Perpetuum.csproj` explicitly excludes `Zones\LootContainers\**` from compilation, yet the files exist in source:
- `src/Perpetuum/Zones/LootContainers/LootContainer.cs`
- `src/Perpetuum/Zones/LootContainers/FieldContainer.cs` (and 5 other files)

**Impact:** These files are dead code that could cause confusion. Any attempt to use these types will fail silently at build time.

**Fix approach:** Either remove the excluded files entirely or restore the compilation and reconcile with the active looting system in `src/Perpetuum/Services/Looting/`.

---

### SQL Injected via Array Interpolation

`ArrayToString()` produces a comma-separated string of IDs that is interpolated directly into SQL `IN (...)` clauses:

- `src/Perpetuum/Accounting/AccountManager.cs:98,232,278`
- `src/Perpetuum/Groups/Alliances/AllianceHelper.cs:19`

These are integer IDs (not user text), so the practical injection risk is low, but the pattern is not safe by construction.

**Fix approach:** Use table-valued parameters or parameterized batch constructs instead of string interpolation.

---

### 673 Inline `Db.Query()` Call Sites

All database access is via a global static `Db.Query()` fluent builder with no repository abstraction enforced across most of the codebase. The 673 call sites are scattered from `Character.cs` to `Market.cs` to `Outpost.cs`.

**Impact:** No single place to add query logging, timeout configuration, retry logic, or connection-level instrumentation. Refactoring the data layer is prohibitively expensive.

**Fix approach:** Gradually migrate high-churn subsystems (Seasons, Mission Engine) to repository interfaces. New code should always route through repositories.

---

### 104 Inline `SELECT *` Queries

`select * from ...` is used extensively (104 occurrences). `Database.CreateLookupCache` and `Database.CreateCache` always issue `select * from {table}`.

**Impact:** Over-fetching, fragile mapping when columns are added/removed, and performance cost on large tables.

**Fix approach:** Enumerate explicit columns in queries; update `Database.cs:33,40` helpers to accept a column list.

---

## Architectural Risks

### XOR Stream Cipher as "Encryption"

The `EncryptedTcpConnection` applies a single-byte XOR rolling cipher to all network traffic:
- `src/Perpetuum/Network/EncryptedTcpConnection.cs`

The cipher uses two hardcoded byte values (`outEncodingByte = 0xCA`, `inDecodingByte = 0xAC`). This is security theater — any network observer can decrypt traffic after seeing a few bytes of known-plaintext.

The per-session RC4 layer added in `ClientConnection` (`src/Perpetuum/Network/ClientConnection.cs`) provides real confidentiality for the admin/test client, but production `ZoneSession` connections do not use this path.

**Impact:** Game traffic between the relay server and zone servers (or between clients and zone ports) is not meaningfully encrypted. Packet sniffing can expose character state and commands.

**Fix approach:** Replace the XOR layer with TLS (`SslStream`) or at minimum per-session AES key exchange using the existing RSA infrastructure.

---

### Passwords Stored and Compared in Plaintext

Account passwords are stored verbatim in the `accounts` table and looked up with a direct SQL equality match:
- `src/Perpetuum/Accounting/AccountRepository.cs:137-143`

There is no hashing (bcrypt, Argon2, PBKDF2) applied anywhere in the authentication path.

**Impact:** A database breach immediately exposes all user credentials. Credential stuffing is trivially possible.

**Fix approach:** Hash passwords with bcrypt or Argon2 on write; verify on read. Migrate existing rows using a forced-reset flow.

---

### No Rate Limiting or Input Validation at Network Boundary

- No per-IP or per-account connection rate limiting beyond a simple `MaxSessions = 5000` cap.
- No brute-force protection on the login command.
- No max packet size enforcement before decryption/decompression (the 8 MB cap in `TcpConnection.cs:12,155` is applied post-length-decode, and a GZip decompress bomb is not guarded against).
- `ClientConnection.OnReceived` decompresses GZip payloads without a decompressed-size limit: `src/Perpetuum/Network/ClientConnection.cs:28`

**Impact:** Brute-force login attacks, connection flooding, and potential decompression-bomb DoS.

**Fix approach:** Add per-IP connection rate limiting in `SessionManager`; add decompressed-size cap before `GZip.Decompress`.

---

### ProcessManager Uses a Custom Game Loop Thread (Not .NET Task Scheduler)

`ProcessManager` runs all zone and service updates in a single `Thread("MainLoop")` with manual `Thread.Sleep`:
- `src/Perpetuum/Threading/Process/ProcessManager.cs`

`ThreadAbortException` is still explicitly caught (`ProcessManager.cs:99`) — a pattern deprecated since .NET Core.

**Impact:** Thread.Abort is no longer thrown in .NET 5+; that catch block is dead code. Any exception in a process `Update()` that is not caught internally will silently swallow the error and advance to the next tick.

**Fix approach:** Remove the `ThreadAbortException` catch. Add per-process exception isolation so one failing process does not corrupt the loop timing.

---

### Fire-and-Forget `Task.Delay().ContinueWith()` Without Cancellation

22 occurrences of fire-and-forget deferred actions (weapon flight time, NPC spawns, beam expiry, SAP enter delays) use `Task.Delay(...).ContinueWith(t => ...)` with no cancellation token:

- `src/Perpetuum/Modules/Weapons/WeaponModule.cs:171,218`
- `src/Perpetuum/Zones/Eggs/AreaBomb.cs:41`
- `src/Perpetuum/Zones/Intrusion/Outpost.cs:233,392,590`
- `src/Perpetuum/Zones/NpcSystem/NPCBossInfo.cs:220`

**Impact:** If a zone shuts down or a unit is destroyed mid-flight, the callback executes against a no-longer-valid zone state. This can cause null reference exceptions or phantom damage.

**Fix approach:** Pass `CancellationToken` from the zone or unit's lifecycle into `Task.Delay`.

---

### Widespread `#if DEBUG` Behavioral Divergence

The `MissionHandler`, `MissionInProgress`, `MissionProcessorDeliverMission`, `ProductionProcessor`, and many other core systems have extensive `#if DEBUG` / `#if !DEBUG` blocks (50+ occurrences) that alter logic, not just logging. The most concerning is `MissionHandler.cs:554`: `#if !DEBUG` wraps a code path that only runs in production.

**Impact:** Release behavior diverges from what developers test locally. Bugs that only manifest in Release builds are harder to reproduce and diagnose.

**Fix approach:** Replace behavioral `#if DEBUG` blocks with configuration flags (`GlobalConfiguration`) or feature flags so they are testable in any build.

---

## Missing Infrastructure

### Partial Test Coverage

A test suite exists (`src/Perpetuum.Tests`, `src/Perpetuum.Tests.Integration`, `tools/smoke-test.ps1`) but covers only the data layer, validation helpers and two regression paths. The subsystems this document calls high-risk are still untested.

**Impact:** Changes to combat calculations, mission rewards, market transactions and season point accrual must still be manually validated in a running server with a connected database. Regressions in those areas remain invisible until they reach production or are found by players.

**Fix approach:** Continue along the coverage map in `IMPROVEMENT-045` — entity system, module state machines, season service, request handlers, mission engine, concurrency. See `docs/codebase/TESTING.md` for what is covered today.

---

### No Structured Logging or Observability

The server uses a custom `Logger` implementation backed by flat file logs and a custom `ILogger<T>` interface. There is no:
- Structured log format (JSON, etc.)
- Correlation IDs across requests
- Distributed tracing
- Metrics (request count, zone tick latency, DB query duration)
- Error aggregation service (Sentry, Seq, etc.)

**Impact:** Diagnosing production issues requires manually tailing flat log files. Latency spikes and error rate trends are invisible.

**Fix approach:** Integrate Serilog with a file sink (structured JSON) and optionally a Seq or Elastic sink. Add zone tick duration metrics.

---

### CI Pipeline Does Not Test

`.github/workflows/dotnet.yml` only builds `Perpetuum.ServerService2`; it does not run tests, static analysis, or linting. The workflow only triggers on the `develop` branch, not `main`.

**Impact:** Pull requests to branches other than `develop` are not validated. No automated quality gate exists.

**Fix approach:** Extend CI to run `dotnet build` on all PRs regardless of target branch. Add `dotnet test` once tests exist. Consider adding Roslyn analyzers.

---

### `Nullable` Set to `annotations` (Not `enable`) Across All Projects

All `.csproj` files use `<Nullable>annotations</Nullable>` rather than `enable`. This means nullable annotations are present but the compiler does not enforce them — `[CanBeNull]` / `[NotNull]` are decorative only.

**Impact:** Null safety is not enforced at compile time. Null dereferences can still occur even where annotations are correct.

**Fix approach:** Migrate to `<Nullable>enable</Nullable>` project by project, fixing warnings incrementally.

---

### Chat Logs Committed to Source Repository

The `src/Perpetuum.ServerService2/data/chatlogs/` directory contains production chat log files from 2024–2025. Git history includes player chat from the `Syndicate Radio` channel.

**Impact:** Player communication data is exposed in the public repository history. Depending on jurisdiction, this may conflict with data privacy obligations.

**Fix approach:** Add `src/Perpetuum.ServerService2/data/` to `.gitignore` and purge historical entries from git history with `git-filter-repo`.

---

## Security Considerations

### Plaintext Passwords in Database
- Risk: Full credential exposure on DB breach.
- Files: `src/Perpetuum/Accounting/AccountRepository.cs`
- Current mitigation: None.
- Recommendation: Implement bcrypt hashing immediately.

### Weak Network Cipher
- Risk: Game traffic decryptable by a passive observer on the network.
- Files: `src/Perpetuum/Network/EncryptedTcpConnection.cs`
- Current mitigation: RC4 layer in `ClientConnection.cs` for admin/test client only.
- Recommendation: TLS at the TCP listener level or per-session AES key agreement.

### No Login Brute-Force Protection
- Risk: Password enumeration via repeated login attempts.
- Files: `src/Perpetuum/Services/Sessions/SessionManager.cs`
- Current mitigation: None (MaxSessions is a total cap, not a per-IP rate limit).
- Recommendation: Track failed logins per IP/account; lock after threshold.

### GZip Decompression Without Size Limit
- Risk: A client can send a small compressed packet that expands to gigabytes (decompression bomb).
- Files: `src/Perpetuum/Network/ClientConnection.cs:28`
- Current mitigation: None.
- Recommendation: Cap decompressed output at a sane maximum (e.g., 4 MB).

### Discord Bot Token in `perpetuum.ini`
- Risk: The `DiscordBotToken` field in `GlobalConfiguration` is stored in plaintext in the `perpetuum.ini` config file inside `GameRoot`. If that directory is accidentally committed or exposed, the bot token leaks.
- Files: `src/Perpetuum/GlobalConfiguration.cs:50`, `src/Perpetuum/Services/EventServices/EventListenerService.cs:123`
- Current mitigation: Config is gitignored at `GameRoot` level.
- Recommendation: Support environment variable override for secrets; document this requirement.

---

## Performance Hotspots

### `SeasonService.RecordActivity` Called on Every Kill / Mine / Mission

`RecordActivity` runs on the hot path for NPC kills, PvP kills, mining, and mission completions. Each call:
1. Iterates `_activeRates` (LINQ `.Where` + `.ToList`)
2. Iterates `_activeObjectives` (LINQ `.Where`)
3. Issues 2–4 DB writes per matched objective (`AddPoints`, `IncrementObjectiveProgress`, `MarkObjectiveBonusAwarded`)
4. Queries `GetClaimedTierIds` per call (full DB round-trip)

- Files: `src/Perpetuum/Services/Seasons/SeasonService.cs:118-165`

**Impact:** Under load with many concurrent kills, this is a significant per-kill DB write amplification.

**Fix approach:** Cache `GetClaimedTierIds` per character in memory; batch `AddPoints` writes; avoid `ToList()` in hot path.

---

### `FastRandom` Uses a Global SpinLock

`FastRandom` is a global singleton with a `SpinLock` protecting its state:
- `src/Perpetuum/FastRandom.cs:18`

It is called 151+ times across zones, combat, and NPC AI — often from concurrent zone threads.

**Impact:** SpinLock contention under multi-zone load. SpinLocks are appropriate only for very short critical sections; if the calling thread is preempted while spinning, CPU is wasted.

**Fix approach:** Use `[ThreadStatic]` per-thread random instances, or switch to `System.Random.Shared` (.NET 6+).

---

### NPC AI Uses `Task.Result` to Block on Pathfinding

Multiple NPC AI classes synchronously block on pathfinding futures using `.Result`:
- `src/Perpetuum/Zones/NpcSystem/AI/CombatAI.cs:297`
- `src/Perpetuum/Zones/NpcSystem/AI/CombatDrones/CombatDroneAI.cs:195`
- `src/Perpetuum/Zones/NpcSystem/AI/FleeAI.cs:107`
- 8 more files

This blocks the zone's `ProcessManager` thread while waiting for pathfinding to complete.

**Impact:** If pathfinding is slow (large zones, complex terrain), the entire zone update loop stalls.

**Fix approach:** Return from AI `Update()` without a path and pick up the result next tick using a stored `Task<List<Point>>`.

---

### 104 `SELECT *` Queries Including Full Table Scans via `Database.CreateCache`

`Database.CreateCache` and `Database.CreateLookupCache` always issue `select * from {table}`:
- `src/Perpetuum/Data/Database.cs:33,40`

These caches are populated at startup and are lazy-loaded, but individual call sites pass large tables (entity definitions, NPC data).

**Impact:** Over-fetching on startup; risk of performance regression if new columns with large data are added to cached tables.

---

## Operational Concerns

### No Graceful Shutdown Procedure

The `ProcessManager.Stop()` forces the game loop thread to terminate with a 5-second join timeout. There is no protocol to:
- Notify connected players of impending shutdown
- Flush in-flight transactions
- Drain the `EventListenerService` queue
- Wait for active Discord async operations to complete

**Impact:** Players can be disconnected mid-transaction on a server restart; item or mission state may be inconsistent.

**Fix approach:** Add a shutdown countdown broadcast (command already exists: `HostShutDownManager`), and wait for transaction draining before stopping the loop.

---

### Configuration Has No Schema Validation

`perpetuum.ini` is deserialized via `JsonConvert.DeserializeObject<GlobalConfiguration>` with no validation. Missing or malformed fields produce null/default values silently:
- `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs:339-340`

**Impact:** Misconfiguration causes cryptic runtime failures far from the config load point.

**Fix approach:** Add required-field validation on `GlobalConfiguration` after deserialization; fail fast at startup with descriptive messages.

---

### Production Chat Logs and Runtime Data in Source Tree

`src/Perpetuum.ServerService2/data/` contains runtime artifacts:
- `chatlogs/` — actual game chat (2024–2025)
- `logs/` — server log files
- `layers/` — zone terrain bitmaps
- `database/` — database-related data

These are checked into git history.

**Impact:** Repository bloat; potential player privacy exposure; difficult to clone for new contributors.

---

### Single-Machine, Windows-Only Deployment

The server explicitly targets `x64` and Windows only. There is no containerization, no horizontal scaling path, and zone processes share the same host. The `Open.Nat` library in the solution suggests NAT traversal is attempted automatically.

**Impact:** No path to cloud deployment or Linux hosting. A single hardware failure takes down all zones.

---

## Recommendations (Prioritized)

1. **[Critical] Hash passwords** — implement bcrypt in `AccountRepository` before any public exposure of the server. This is a blocking security requirement.

2. **[High] Add decompression bomb guard** — cap decompressed packet size in `ClientConnection.OnReceived` to prevent DoS.

3. **[High] Add login rate limiting** — track failed auth attempts per IP/account in `SessionManager`; block after a configurable threshold.

4. **[High] Remove production data from git** — purge `src/Perpetuum.ServerService2/data/chatlogs/` and `data/logs/` from git history; add to `.gitignore`.

5. **[High] Provide cancellation tokens to `Task.Delay` fire-and-forget calls** — prevents phantom callbacks after zone teardown.

6. **[Medium] Replace `ThreadAbortException` catch** in `ProcessManager` — it is dead code on .NET 8 and masks real issues.

7. **[Medium] Cache `GetClaimedTierIds` per character** in `SeasonService.RecordActivity` — reduces per-kill DB write amplification.

8. **[Medium] Replace `FastRandom` global SpinLock** with `[ThreadStatic]` instances or `System.Random.Shared`.

9. **[Medium] Migrate nullable to `enable`** project by project — enforces null safety at compile time.

10. **[Low] Fix NPC AI pathfinding blocking** — store `Task<List<Point>>` across ticks instead of blocking on `.Result`.

11. **[Low] Move hardcoded economy constants to DB** — alpha/beta multipliers, mission reward parameters flagged with TODO.

12. **[Low] Add startup config validation** for `GlobalConfiguration` — fail fast on missing required fields.

13. **[Low] Introduce structured logging** (Serilog) — prerequisite for meaningful observability.

14. **[Low] Add unit tests for pure-logic modules** — season point math, mission reward formulas, economy calculations — as a foundation for future confidence.

---

*Concerns audit: 2026-05-11*
