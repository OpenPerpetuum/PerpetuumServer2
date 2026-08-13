# Testing

**Analysis Date:** 2026-05-11

## Current State

**No automated test suite exists in this repository.**

There is no xUnit, NUnit, MSTest, or any other test framework. The CI pipeline (`.github/workflows/dotnet.yml`) runs only `dotnet build` and `dotnet restore` — no `dotnet test` step exists. There are no `*.Tests` projects, no test discovery configuration, and no code coverage tooling.

## Test Infrastructure

None. There is no test runner, no assertion library, no mock framework, and no test helpers.

Files with "Test" in their name are not unit tests — they are **in-game admin commands** that exercise game logic against a live database while the server is running:

| File | What it does |
|------|-------------|
| `src/Perpetuum.RequestHandlers/Extensions/ExtensionTest.cs` | Triggers extension-point grant and measures SQL execution time |
| `src/Perpetuum.RequestHandlers/Zone/ZonePBSTest.cs` | Resets PBS highway bits on a live zone terrain |
| `src/Perpetuum.RequestHandlers/Zone/ZoneTerraformTest.cs` | In-game terraform operation test |
| `src/Perpetuum.RequestHandlers/Missions/MissionResolveTest.cs` | Admin command that invokes `MissionResolveTester` against live DB |
| `src/Perpetuum/Services/MissionEngine/MissionResolveTester.cs` | Parallel mission resolve batch-runner; writes results to `missiontolocation` and `missiontargetslog` tables |
| `src/Perpetuum/Services/MissionEngine/OneLocationTest.cs` | Single-location mission resolve helper used by `MissionResolveTester` |

These are dispatched exactly like player commands (via `IRequestHandler`) with `AccessLevel.admin` and require a live game server with a connected SQL Server database. They are not isolated or repeatable without the full runtime.

## Manual Testing

The team tests by running the server locally against a configured `GameRoot`:

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

The `Perpetuum.AdminTool` project (`src/Perpetuum.AdminTool/`) provides a GUI tool for administrative operations including the Seasons Admin Tool.

## CI Pipeline

`.github/workflows/dotnet.yml` runs on pushes and pull requests to `develop` branch only:

```yaml
- dotnet restore
- dotnet build src/Perpetuum.ServerService2/... --configuration Release -p:Platform=x64
```

A build artifact is uploaded on successful push. No tests are executed in CI.

## Gaps

**Everything is untested at the automated level.** Specific high-risk gaps:

- **Entity system** (`src/Perpetuum/EntityFramework/`) — `Entity`, `EntityDefault`, `EntityDynamicProperties` have no isolation tests. These underpin all game objects.
- **Module state machines** (`src/Perpetuum/Modules/ActiveModule.States.cs`) — state transitions for robot equipment are tested only by playing the game.
- **Guard/validation extensions** (`src/Perpetuum/Guard.cs`) — the `ThrowIf*` extension methods are used pervasively but never unit-tested.
- **Database query layer** (`src/Perpetuum/Data/DbQuery.cs`, `Db.cs`) — no integration test harness for SQL query correctness.
- **Request handlers** (`src/Perpetuum.RequestHandlers/`) — 200+ handler classes have no test doubles or mock session/request infrastructure.
- **Concurrent/threading code** — `ProcessManager`, `MessageSender`, `TcpConnection` use `ThreadPool` and `Task.Run` patterns that are notoriously difficult to test without a harness.
- **Season service logic** (`src/Perpetuum/Services/Seasons/SeasonService.cs`) — tier grant, objective completion, leaderboard delivery, intro mail idempotency, and end-of-season processing are exercised only via live play.
- **Mission engine** (`src/Perpetuum/Services/MissionEngine/`) — the most complex subsystem; the existing `MissionResolveTester` exercises resolve logic but requires a live DB and has no assertions — it logs results to tables for human inspection.

## Adding Tests (If Introduced)

To add automated tests, the recommended path would be:

1. Create a `Perpetuum.Tests` project (xUnit recommended for .NET 8)
2. Add project reference to `Perpetuum` core library
3. The main obstacle is the pervasive use of static service locators (`Entity.Services`, `EntityDefault.Reader`, `Db.DbQueryFactory`, `Logger.Current`) — these would need to be initialized or replaced with test doubles before any entity or database code can run in isolation
4. `Guard.cs` extension methods and `ValueTypeExtensions.cs` are pure functions with no dependencies — good first candidates for unit tests
5. `SeasonRepository` methods are testable with an in-memory or test SQL database since they only use `Db.Query()` which is factory-injected

---

*Testing analysis: 2026-05-11*
