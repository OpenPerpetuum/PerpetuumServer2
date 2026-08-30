# Insurance System Overhaul — Design Spec

**Date:** 2026-06-06
**Backlog:** IMPROVEMENT-036
**Branch target:** p36.5

---

## Overview

The insurance system is mechanically sound but economically inert: `insuranceprices` data is stale and too low to produce meaningful fees or payouts, the fee extension (`ext_production_insurance_fee`) is wired up in configuration but never applied at purchase time, and two global multiplier fields (`InsuranceFeeMultiplier`, `InsurancePayOutMultiplier`) exist in `InsuranceHelper` but are never read.

This overhaul ties insurance pricing to production costs via a daily refresh job and configurable percentage parameters, wires the dead extension bonus into the purchase flow, and ships a pre-go-live migration that clears stale policies.

---

## Goals

- Insurance fee and payout are dynamically computed from actual production cost
- Prices refresh daily (automatic) and on demand (Admin Tool trigger)
- Insurance is always a guaranteed NIC sink: `fee_pct > payout_pct` enforced by the SP
- Fee extension bonus (`ext_production_insurance_fee`) is applied at purchase time
- Dead static multiplier fields removed
- All stale policies cleared before go-live so players repurchase at correct rates

---

## Section 1: Data Layer

### `insurance_config` table

New table, same `param_name` / `param_value` pattern as `automarket_config`.

| `param_name` | `param_value` | Description |
|---|---|---|
| `fee_pct` | `0.10` | Fee = 10% of production cost (operator-tunable) |
| `payout_pct` | `0.08` | Payout = 8% of production cost (must remain < `fee_pct`) |

Created with `IF OBJECT_ID IS NULL` guard; seed values inserted on creation.

### `usp_RecalculateInsurancePrices` stored procedure

Logic:
1. Read `fee_pct` and `payout_pct` from `insurance_config`
2. Guard: `RAISERROR` if `payout_pct >= fee_pct` — prevents misconfiguration that would make insurance a NIC source
3. Join `v_all_production_costs` → `entitydefaults` (on `definitionname`) → `insuranceprices` (on `definition`)
4. MERGE into `insuranceprices`: `fee = production_cost_nic × fee_pct`, `payout = production_cost_nic × payout_pct`
5. Definitions present in `insuranceprices` but absent from `v_all_production_costs` (no production chain) are skipped — existing values preserved

Created with `CREATE OR ALTER PROCEDURE`.

### `insuranceprices` table

No schema changes. The SP writes to existing `fee` and `payout` columns.

---

## Section 2: Server Runtime

### `InsurancePriceRefreshService : IProcess`

New service, same pattern as `EconomySnapshotService`.

- **On startup:** executes `usp_RecalculateInsurancePrices`, then calls `InsuranceHelper.LoadInsurancePrices()` to warm the in-memory cache
- **Daily:** same sequence on a 24-hour timer
- Registered in Autofac alongside `EconomySnapshotService`

### Dead code fixes — `InsuraceFacility` and `InsuranceHelper`

**Wire fee extension bonus** in `InsuraceFacility.InsuranceBuy`:

```csharp
double insuranceFee, payOut;
GetInsurancePrice(robot, out insuranceFee, out payOut).ThrowIfError();

var feeBonus = GetFeeExtensionBonus(character);
insuranceFee = Math.Max(0, insuranceFee * (1.0 - feeBonus));
```

> **Implementation note:** verify the unit returned by `ext_production_insurance_fee` via `GetExtensionsBonusSummary` before applying the formula — confirm it is a fraction in [0, 1] (e.g. 0.05 per level) rather than a flat NIC amount or a percentage integer.

**Remove dead static fields** from `InsuranceHelper`:
- `public static double InsuranceFeeMultiplier = 1.0;`
- `public static double InsurancePayOutMultiplier = 0.90;`

Both are superseded by `fee_pct` / `payout_pct` in `insurance_config`.

### Cache invalidation

After `usp_RecalculateInsurancePrices` runs (scheduled or manual), `InsuranceHelper.LoadInsurancePrices()` is called immediately to flush and repopulate `_insurancePrices`. No server restart required.

---

## Section 3: Admin Tool

### New "Insurance" tab — Economy panel (Tab 5)

Added to the existing `EconomyViewModel` tab collection alongside NIC Flow, Money Supply, Market Health, and Sink Effectiveness.

**New files:**
- `EconomyInsuranceViewModel.cs`
- `EconomyInsuranceView.xaml` / `.xaml.cs`

#### Config sub-section (top of tab)

- Two editable rows: `fee_pct` and `payout_pct`, displayed as human-readable percentages
- Changes queued via `ChangeQueue`, same pattern as AutoMarket Config tab
- Inline warning shown (non-blocking) when `fee_pct ≤ payout_pct`

#### Price table (read-only)

Columns: Item Name (translated, fallback to `definitionname`), Production Cost (NIC), Fee, Payout.

Sourced from `insuranceprices` joined to `entitydefaults` and the translations system. Refreshes on tab activation and after "Recalculate Now".

#### "Recalculate Now" toolbar button

- Calls `usp_RecalculateInsurancePrices` directly over the Admin Tool DB connection — no server-side handler needed (same approach as AutoMarket)
- Disabled while running
- Surfaces SP errors (including `payout_pct ≥ fee_pct` guard) in a visible error message
- On success: reloads the price table

---

## Section 4: Pre-go-live Migration

**File:** `docs/db_structure/migrations/IMPROVEMENT-036-insurance-overhaul.sql`

Execution order:

1. Create `insurance_config` (IF NOT EXISTS guard) and seed default values
2. `CREATE OR ALTER PROCEDURE usp_RecalculateInsurancePrices`
3. `DELETE FROM insurance` — clears all active policies with stale payout values; players repurchase at new rates after go-live
4. `EXEC usp_RecalculateInsurancePrices` — populates `insuranceprices` immediately so the server cache loads correct values on first startup

**Operator note:** run this migration while the server is offline or before restarting with the new build.

---

## Affected Files

| File | Change |
|---|---|
| `src/Perpetuum/Services/Insurance/InsuranceHelper.cs` | Remove `InsuranceFeeMultiplier`, `InsurancePayOutMultiplier` |
| `src/Perpetuum/Services/ProductionEngine/Facilities/InsuraceFacility.cs` | Wire `GetFeeExtensionBonus` into fee calculation |
| `src/Perpetuum/Services/Insurance/InsurancePriceRefreshService.cs` | New file |
| `src/Perpetuum.Bootstrapper/PerpetuumBootstrapper.cs` | Register `InsurancePriceRefreshService` (same block as `EconomySnapshotService`) |
| `src/Perpetuum.AdminTool/ViewModels/EconomyInsuranceViewModel.cs` | New file |
| `src/Perpetuum.AdminTool/Views/EconomyInsuranceView.xaml` | New file |
| `src/Perpetuum.AdminTool/ViewModels/EconomyViewModel.cs` | Wire Insurance tab |
| `docs/db_structure/migrations/IMPROVEMENT-036-insurance-overhaul.sql` | New migration |

---

## Risks and Constraints

- `v_all_production_costs` only covers craftable items (`purchasable=1, enabled=1, hidden=0`). Robot definitions outside the production chain retain their existing `insuranceprices` values unchanged.
- `InsurancePriceRefreshService` fires at startup — if `insurance_config` doesn't exist yet (migration not run), the SP will fail. Server must not start with the new build before the migration is applied.
- Removing `InsuranceFeeMultiplier` / `InsurancePayOutMultiplier`: grep for any external references before deletion (unlikely given no usages found, but verify).
- Fee extension bonus formula requires runtime verification of the extension's bonus unit before implementation.

---

## Manual Validation Steps

1. Run migration on a test DB; confirm `insurance_config` seeded, `insuranceprices` populated with non-zero values
2. Start server; confirm `InsurancePriceRefreshService` logs a successful run on startup
3. Navigate to Admin Tool → Economy → Insurance; confirm price table shows expected values
4. Edit `fee_pct` / `payout_pct`, queue and commit; click Recalculate Now; confirm price table updates
5. Set `payout_pct ≥ fee_pct`; click Recalculate Now; confirm error is shown and table is unchanged
6. Buy insurance on a robot in-game; confirm fee charged matches `production_cost × fee_pct` (minus extension bonus if applicable)
7. Test insurance payout on robot death in a test zone; confirm payout matches `insuranceprices.payout` for that definition
8. Confirm character with `ext_production_insurance_fee` trained pays a reduced fee vs. a character without it
