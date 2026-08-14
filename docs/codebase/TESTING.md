# Testing

**Analysis Date:** 2026-08-14

## Current State

The repository has an automated test suite in three tiers. It does not cover the whole codebase — the
coverage map below states what is covered and what is not.

| Tier | Project | Count | Needs |
|------|---------|-------|-------|
| 1 — smoke | `tools/smoke-test.ps1` | 1 end-to-end run | A configured `GameRoot` and a live database |
| 2 — unit | `src/Perpetuum.Tests` | 58 tests | Nothing. Runs anywhere the solution builds |
| 3 — integration | `src/Perpetuum.Tests.Integration` | 8 tests | A configured `GameRoot` and a live database |

Tier 2 is the tier that runs in CI. Tiers 1 and 3 run on a developer machine that already has the
standard server environment, and skip rather than fail when it is absent.

## Running the tests

Tier 2, no setup required:

```bash
dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64
```

Tier 3, against the real database:

```bash
set PERPETUUM_GAMEROOT=C:\PerpetuumServer\data
dotnet test src/Perpetuum.Tests.Integration/Perpetuum.Tests.Integration.csproj -c Release -p:Platform=x64
```

Tier 1, a full server run:

```bash
pwsh tools/smoke-test.ps1 -GameRoot C:\PerpetuumServer\data
```

### Environment variables

| Variable | Read by | Effect |
|----------|---------|--------|
| `PERPETUUM_GAMEROOT` | Tiers 1 and 3 | Directory holding `perpetuum.ini`. Unset, every tier-3 test that touches the database is **skipped**, not failed |
| `PERPETUUM_TESTDB_ALLOW_WRITE` | Tier 3 | Set to `1` to opt in to tests that write. Unset, only read-only tests run |

Tier 3 does not carry its own connection string. It deserializes the same `perpetuum.ini` into the same
`GlobalConfiguration` type the bootstrapper uses, so the connection string cannot drift from the one
the server runs with.

## Test Infrastructure

### Tier 2 — unit

Framework: xUnit v3, with NSubstitute for interface doubles.

The four static service locators the previous version of this document called "the main obstacle" are
the seams the suite uses. Each is a settable `public static` property assigned in exactly one place, so
a fixture assigns a double before exercising code:

| Seam | Type | Production assignment |
|------|------|-----------------------|
| `Logger.Current` | `ILogger<LogEvent>` | `PerpetuumBootstrapper.cs:137` |
| `Db.DbQueryFactory` | `Func<DbQuery>` | `PerpetuumBootstrapper.cs:150` |
| `EntityDefault.Reader` | `IEntityDefaultReader` | `PerpetuumBootstrapper.cs:156` |
| `Entity.Services` | `IEntityServices` | `PerpetuumBootstrapper.cs:157` |

The data layer is the important one. `Db.Query()` funnels 761 call sites through
`Db.DbQueryFactory`, and `DbQuery` takes a `DbConnectionFactory` delegate, so
`Db.DbQueryFactory = () => new DbQuery(() => fakeConnection)` intercepts all of them without any
production change.

`Fakes/Data/` implements the ADO.NET interfaces as a recording fake: a test registers a result set
against a command pattern, then asserts on the SQL and parameters the code under test actually
produced. `Fakes/RecordingLogger.cs` does the same for log output.

Because these seams are process-wide static state, the fixtures live in xUnit collections
(`PerpetuumStaticsCollection`) so classes touching them do not run in parallel with each other.

### Tier 3 — integration

Runs against the real `perpetuumsa`. No synthetic schema is built and no database is created,
restored or snapshotted: every developer who touches this code already has the standard environment,
and a second copy of the DDL would drift from production.

Two things are covered:

- **Schema conformance** — every stored procedure and function documented under `docs/db_structure/`
  is checked to exist in the live database with the documented parameter signature.
- **Query anchoring** — the queries that tier 2 stubs are executed against the real schema, so the
  fake stays an assertion about how the database actually behaves rather than about how it was
  imagined to behave.

Isolation is by read-only default: writes require `PERPETUUM_TESTDB_ALLOW_WRITE=1`. Tests use a single
connection, because a second concurrent connection inside a `TransactionScope` escalates to MSDTC.

### Tier 1 — smoke

`tools/smoke-test.ps1` builds the solution, starts the real server, waits for `State : [Online]`, waits
for the log to quiesce, sends Ctrl+C through `GenerateConsoleCtrlEvent`, and asserts the process
reaches `State : [Off]` and exits 0. Force-killing a server that will not stop is reported as a
failure, not a pass.

Assertions are in three categories, declared in arrays at the top of the script:

| Category | Behaviour |
|----------|-----------|
| Required | Absence fails the run — `State : [Online]`, `State : [Off]` |
| Forbidden | Presence fails the run — exception signatures in the startup path |
| Reported | Printed, never asserted — flock count, members spawned, time to online |

The third category is deliberate. Across recorded runs the spawned member count was 6406, 6423 and
6425; it changes with every content patch, so asserting on it would build a test that fails when the
game works.

Exit codes: `0` pass, `2` build failed, `3` GameRoot not found, `4` timed out waiting for online,
`5` forbidden pattern in the log, `6` shutdown was not graceful, `7` unexpected error.

## Regression tests

Two tests in `src/Perpetuum.Tests/Regression/` exist because a specific bug reached production:

| Test | Guards |
|------|--------|
| `Issue033EmptyFlockTests` | `FreeRoamingPathFinder` throwing on a presence with no flocks |
| `Issue039InsuranceTransactionTests` | `LoadInsurancePrices()` running inside an already-completed `TransactionScope`, which left the price cache stale |

Both were **observed failing with their fixes reverted** before being accepted. A regression test that
has never been seen red is a statement about nothing.

## Files that are not tests

Files with "Test" in their name in the production projects are **in-game admin commands**, not
automated tests. They are dispatched like player commands via `IRequestHandler` with
`AccessLevel.admin` and need a live server:

| File | What it does |
|------|-------------|
| `src/Perpetuum.RequestHandlers/Extensions/ExtensionTest.cs` | Triggers extension-point grant and measures SQL execution time |
| `src/Perpetuum.RequestHandlers/Zone/ZonePBSTest.cs` | Resets PBS highway bits on a live zone terrain |
| `src/Perpetuum.RequestHandlers/Zone/ZoneTerraformTest.cs` | In-game terraform operation test |
| `src/Perpetuum.RequestHandlers/Missions/MissionResolveTest.cs` | Admin command that invokes `MissionResolveTester` against live DB |
| `src/Perpetuum/Services/MissionEngine/MissionResolveTester.cs` | Parallel mission resolve batch-runner; writes results to `missiontolocation` and `missiontargetslog` |
| `src/Perpetuum/Services/MissionEngine/OneLocationTest.cs` | Single-location mission resolve helper used by `MissionResolveTester` |

## Manual Testing

Automated tiers do not replace running the server. For anything touching gameplay, the team still tests
by running locally against a configured `GameRoot`:

```bash
cd src/Perpetuum.Server
dotnet run -- "E:\PerpetuumServer2\data"
```

Manual verification involves:
- Connecting a game client to the server
- Exercising features through normal gameplay or admin commands
- Sending admin commands (e.g. `MissionResolveTest`, `ExtensionTest`) via client or a tool
- Inspecting server console log output (`Logger.Info/Warning/Error`) and file logs under `logs/`
- Querying the SQL Server database directly to verify data state

The `Perpetuum.AdminTool` project (`src/Perpetuum.AdminTool/`) provides a GUI tool for administrative
operations including the Seasons Admin Tool.

## CI Pipeline

`.github/workflows/dotnet.yml` runs on pushes and pull requests to `develop` only. It has four jobs:

| Job | What it runs |
|-----|--------------|
| `build` | `dotnet build src/Perpetuum.ServerService2/...` and uploads the artifact on push |
| `test` | `dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj` — tier 2 only |
| `build-admintool-installer` | Builds the AdminTool MSI |
| `publish-wiki` | Publishes the graphify codebase report |

The `test` job does not reference the integration project, and `[RequiresGameRoot]` would skip it even
if something did. Two independent barriers, because one alone is a convention.

## Gaps

Covered so far: `Guard.cs`, `ValueTypeExtensions.cs`, the database query layer, and the two regression
paths above. Still untested at the automated level:

- **Entity system** (`src/Perpetuum/EntityFramework/`) — `Entity`, `EntityDefault`, `EntityDynamicProperties` have no isolation tests. These underpin all game objects.
- **Module state machines** (`src/Perpetuum/Modules/ActiveModule.States.cs`) — state transitions for robot equipment are tested only by playing the game.
- **Request handlers** (`src/Perpetuum.RequestHandlers/`) — 200+ handler classes have no test doubles or mock session/request infrastructure.
- **Concurrent/threading code** — `ProcessManager`, `MessageSender`, `TcpConnection` use `ThreadPool` and `Task.Run` patterns that need a harness before they can be tested deterministically.
- **Season service logic** (`src/Perpetuum/Services/Seasons/SeasonService.cs`) — tier grant, objective completion, leaderboard delivery, intro mail idempotency, and end-of-season processing are exercised only via live play.
- **Mission engine** (`src/Perpetuum/Services/MissionEngine/`) — the most complex subsystem; `MissionResolveTester` exercises resolve logic but requires a live DB and has no assertions.

Covering every file is not the goal. See `IMPROVEMENT-045` in `docs/backlog/improvements.md` for the
planned order of attack.

## Adding Tests

1. Pure functions and validation helpers go in `src/Perpetuum.Tests/Unit/`.
2. Anything that reaches the database goes through the fake in `Fakes/Data/`. Register the result set,
   exercise the code, assert on the SQL and parameters it produced.
3. Anything that needs the real schema goes in `src/Perpetuum.Tests.Integration` behind
   `[RequiresGameRootFact]`, so it skips on a machine without the environment instead of failing.
4. A test written for a bug must be observed failing before the fix is applied, or reverted against
   afterwards. Otherwise it proves nothing.
5. Production code is not restructured to make a test possible. The four seams above have been enough
   so far; if a test genuinely cannot be written without a new seam, that is a discussion to have in
   the pull request, not a refactor to slip in.

---

*Testing analysis: 2026-08-14*
