# Code Coverage

**Analysis Date:** 2026-09-10

## Scope

This document records the automated code coverage metrics for the backend and server codebase (`src/`), excluding the standalone client directory (`client/`).

Measurements were performed inside the SDK test container using `dotnet-coverage` (Microsoft code coverage tools) and `ReportGenerator` against the net8.0 test suite.

---

## Executive Summary

| Scope | Line Coverage % | Covered / Coverable Lines | Branch Coverage % | Method Coverage % | Total Code Lines |
|---|---|---|---|---|---|
| **Production Code (CI Unit Tests)** | **9.2%** | 6,882 / 74,118 | **3.7%** (870 / 23,075) | **3.6%** (436 / 11,891) | 166,665 |
| **Production Code (Unit + Integration Tests)** | **9.5%** | 7,049 / 74,118 | **3.9%** (906 / 23,075) | **4.0%** (481 / 11,891) | 166,665 |
| **All Instrumented Assemblies (incl. Unit Test Suite)** | **11.3%** | 8,653 / 75,974 | **4.6%** (1,094 / 23,311) | **5.6%** (693 / 12,229) | 170,489 |
| **Full C# Solution (all 9 non-test projects in `src/`)** | **~8.3%** | 7,049 / ~85,000 | **~3.4%** | **~3.5%** | ~204,000 |

---

## Per-Assembly Breakdown

### Production Assemblies

| Project / Assembly | Line Coverage | Branch Coverage | Source Files | Lines of Code | Description / Coverage Focus |
|---|---|---|---|---|---|
| `Perpetuum` | **11.0%** | **4.4%** | 1,337 files | ~142,800 | Core domain logic. Covered areas: geometry (`Area`, `PointExtensions`), SIMD math (`SimdMath`), data access fakes (`Db`, `DbQuery`), logging, bitmaps/layers. |
| `Perpetuum.CliClient` | **19.4%** | **15.0%** | 17 files | ~2,880 | CLI client helper commands, connection activity, authentication protocols. |
| `Perpetuum.RequestHandlers` | **0.4%** | **0.0%** | 585 files | ~26,500 | 200+ command handlers. Largely untested in isolation at the unit level; orchestrate zone/service logic. |
| `Perpetuum.ExportedTypes` | **0.0%** | **0.0%** | 14 files | ~7,670 | Enums, category flags, definition IDs, aggregate field constants. Declarative data without complex logic. |
| `Perpetuum.Bootstrapper` | **0.0%** | **0.0%** | 24 files | ~3,430 | Autofac dependency injection modules and container bootstrap wiring. |
| `Perpetuum.AdminTool` | **0.0%** | **0.0%** | 234 files | ~16,880 | WinForms / WPF server administration tool. |
| `Perpetuum.Server` | **0.0%** | **0.0%** | 1 file | ~110 | Console server entry point (`Program.cs`). |
| `Perpetuum.ServerService2` | **0.0%** | **0.0%** | 2 files | ~120 | Windows Service host entry point. |
| `Open.Nat` | **0.0%** | **0.0%** | 35 files | ~3,850 | Third-party UPnP / NAT-PMP traversal library. |

### Test Assemblies

| Test Project | Line Coverage | Tests Count | Lines of Code | Role |
|---|---|---|---|---|
| `Perpetuum.Tests` | **95.3%** | 209 unit tests | ~3,840 | Tier 2 automated unit test suite, test seams, in-memory recording fakes. |
| `Perpetuum.Tests.Integration` | **N/A** | 15 integration tests | ~820 | Tier 3 schema conformance & live DB queries. |

---

## Subsystem Coverage Map

### Well-Covered Subsystems (>80% coverage)
- **Math & Spatial Utilities:** `Area`, `SimdMath`, `PointExtensions`, `SizeExtensions`, `CompactPassabilityMask`, `SpatialCollectionsSkia`.
- **Data Query Abstraction:** `DbQuery`, `DbConnectionManager`, `ConnectionStringSupport`, recording fake ADO.NET providers.
- **Guard & Validation Extensions:** `Guard.cs`, `ValueTypeExtensions.cs`.
- **Image & Layer Utilities:** `BitmapExtensions`, `BinaryStreamSkia`, `LayerExtensions`, `HeightfieldMetadata`.
- **Known Regression Guards:** `Issue033EmptyFlockTests`, `Issue039InsuranceTransactionTests`.

### Untested Subsystems (0% - 5% coverage)
- **Entity System (`src/Perpetuum/EntityFramework/`):** `Entity`, `EntityDefault`, `EntityDynamicProperties`, entity factories.
- **Module State Machines (`src/Perpetuum/Modules/`):** `ActiveModule.States`, combat equipment state transitions.
- **Request Handlers (`src/Perpetuum.RequestHandlers/`):** Over 200 client command handlers.
- **Zone Runtime & Threading (`src/Perpetuum/Zones/`, `src/Perpetuum/Threading/`):** `ProcessManager`, `Zone`, `ZoneSession`, movement, combat loops, NPC AI.
- **Game Services (`src/Perpetuum/Services/`):** `MissionEngine`, `SeasonService`, `MarketEngine`, `ProductionEngine`, `Looting`, `Social`, `Mail`.

---

## Reproducing Coverage Reports

Code coverage can be generated in the test Docker container using the commands below:

### Generate Production Coverage (Excluding Tests)

```bash
./script/compose.sh --profile test run --no-deps --rm test sh -c '
export PATH="$PATH:/root/.dotnet/tools"
dotnet tool install -g dotnet-coverage >/dev/null 2>&1 || true
dotnet tool install -g dotnet-reportgenerator-globaltool >/dev/null 2>&1 || true

dotnet-coverage collect "dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --no-build" \
  -f cobertura -o /tmp/coverage.cobertura.xml

reportgenerator -reports:/tmp/coverage.cobertura.xml \
  -targetdir:/tmp/CoverageReport \
  -reporttypes:"TextSummary;Html" \
  -assemblyfilters:"-Perpetuum.Tests;-Perpetuum.Tests.*"

cat /tmp/CoverageReport/Summary.txt
'
```

### Generate Merged Unit + Integration Coverage

```bash
./script/compose.sh --profile test run --no-deps --rm test sh -c '
export PATH="$PATH:/root/.dotnet/tools"
dotnet tool install -g dotnet-coverage >/dev/null 2>&1 || true
dotnet tool install -g dotnet-reportgenerator-globaltool >/dev/null 2>&1 || true

dotnet-coverage collect "dotnet test src/Perpetuum.Tests/Perpetuum.Tests.csproj -c Release -p:Platform=x64 --no-build" \
  -f cobertura -o /tmp/cov_unit.cobertura.xml

dotnet-coverage collect "dotnet test src/Perpetuum.Tests.Integration/Perpetuum.Tests.Integration.csproj -c Release -p:Platform=x64 --no-build" \
  -f cobertura -o /tmp/cov_int.cobertura.xml

reportgenerator -reports:/tmp/cov_*.cobertura.xml \
  -targetdir:/tmp/CoverageReportMerged \
  -reporttypes:"TextSummary" \
  -assemblyfilters:"-Perpetuum.Tests;-Perpetuum.Tests.*"

cat /tmp/CoverageReportMerged/Summary.txt
'
```
