# Technology Stack

**Analysis Date:** 2026-05-11

## Languages & Runtimes

**Primary:**
- C# 12 / .NET 8 — all server and tool projects
- SQL (T-SQL) — stored procedures, views, functions in `docs/db_structure/`

**Secondary:**
- XAML — WPF Admin Tool UI (`src/Perpetuum.AdminTool/`)

**Runtime:**
- .NET 8 (`net8.0`), except Admin Tool which targets `net8.0-windows`
- Platform: x64 only (all `.csproj` files set `<Platforms>x64</Platforms>`)
- OS: Windows only (bootstrapper annotated `[SupportedOSPlatform("windows")]`)
- GC: Server GC enabled in `Perpetuum.ServerService2` (`<ServerGarbageCollection>true</ServerGarbageCollection>`)

## Frameworks & Libraries

**Dependency Injection:**
- `Autofac` 8.2.0 — entire DI container; registered in 19 `Modules/*.cs` under `src/Perpetuum.Bootstrapper/Modules/`

**Windows Service Host:**
- `Microsoft.Extensions.Hosting` 9.0.1 — generic host abstraction
- `Microsoft.Extensions.Hosting.WindowsServices` 9.0.1 — Windows service integration in `Perpetuum.ServerService2`

**Database Access:**
- `Microsoft.Data.SqlClient` 6.0.1 — raw ADO.NET; no ORM. Used in `src/Perpetuum/Data/DbQuery.cs` and `src/Perpetuum.AdminTool/`

**Serialization:**
- `Newtonsoft.Json` 13.0.3 — JSON config parsing (`GlobalConfiguration`, `perpetuum.ini`)
- Custom binary protocol: GenXY (`src/Perpetuum/GenXY/`) — `GenxyReader`, `GenxyWriter`, `GenxyConverter`, `GenxyToken`

**Discord Integration:**
- `Discord.Net` 3.17.4 — bot client (`DiscordSocketClient`) for bidirectional chat bridge; see `src/Perpetuum/Services/EventServices/EventListenerService.cs`

**WPF / Admin Tool:**
- `CommunityToolkit.Mvvm` 8.3.2 — MVVM helpers for `src/Perpetuum.AdminTool/`

**Networking / NAT:**
- `SharpOpenNat` 4.0.17 — UPnP port mapping; optional, controlled by `GlobalConfiguration.EnableUpnp`

**Utilities:**
- `DeepCloner` 0.10.4 — deep object cloning in core game logic
- `System.Drawing.Common` 9.0.1 — terrain bitmap processing in `src/Perpetuum/Zones/`
- `System.Runtime.Caching` 9.0.1 — `ObjectCache` for character and entity caching
- `System.Net.Http` 4.3.4 — HTTP post utility in `src/Perpetuum/Network/Http.cs`
- `System.Text.RegularExpressions` 4.3.1 — regex utilities (referenced in all projects)

**CLI:**
- `McMaster.Extensions.CommandLineUtils` 4.1.1 — positional `<GAMEROOT>` argument parsing in `src/Perpetuum.Server/Program.cs`

**Crypto:**
- RC4 stream cipher: custom implementation at `src/Perpetuum/Rc4.cs`
- RSA: custom implementation at `src/Perpetuum/Rsa.cs`
- XOR stream cipher: custom; see `src/Perpetuum/Network/EncryptedTcpConnection.cs`

## Build & Tooling

**Build System:**
- `dotnet build` with MSBuild
- Solution file: `PerpetuumServer2.sln`
- Release command: `dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64`
- Output directory: `bin/x64/Release/net8.0` (set via `<BaseOutputPath>..\..\bin</BaseOutputPath>` in `Perpetuum.ServerService2`)

**CI/CD:**
- GitHub Actions: `.github/workflows/dotnet.yml`
- Runner: `windows-latest`
- Trigger: push/PR to `develop` branch
- Publishes build artifact `Perpetuum-Server-v2-{sha}` on push
- Only `Perpetuum.ServerService2` project is built in the `build` job
- A `test` job runs the unit tier; the integration tier is not referenced in CI

**Testing:**
- Framework: xUnit v3, with NSubstitute for interface doubles
- `src/Perpetuum.Tests` — unit tier, no external dependencies, runs in CI
- `src/Perpetuum.Tests.Integration` — runs against the real database, skipped when `PERPETUUM_GAMEROOT` is unset
- `tools/smoke-test.ps1` — builds, starts the server, asserts on the startup and shutdown log
- Coverage is partial by design; see `docs/codebase/TESTING.md`

**Unsafe code:**
- `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in `src/Perpetuum/Perpetuum.csproj`

## Package Management

**Package manager:** NuGet (standard `<PackageReference>` in `.csproj` files)
**Lockfile:** Not detected (no `packages.lock.json` committed)
**Legacy packages folder:** `packages/` directory exists at repo root (may be legacy)

## Database

**Engine:** Microsoft SQL Server
**Access pattern:** Raw ADO.NET via custom `DbQuery` fluent builder — no ORM
- `src/Perpetuum/Data/DbQuery.cs` — query builder with `CommandText`, `SetParameter`, `Execute`
- `src/Perpetuum/Data/Db.cs` — static factory facade (`Db.Query()`, `Db.CreateTransaction()`)
- `src/Perpetuum/Data/Database.cs` — `LazyDictionary` / `LazyLookup` table-cache utilities
- Auto-detects stored procedures vs inline SQL: command text without spaces → `CommandType.StoredProcedure`
- Transaction isolation: `ReadCommitted` via `TransactionScope`; distributed transactions enabled (`TransactionManager.ImplicitDistributedTransactions = true`)
- Schema docs: `docs/db_structure/` (authoritative; see CLAUDE.md)

---

*Stack analysis: 2026-05-11*
