# IMPROVEMENT-030 — AutoMarket Overhaul Design

**Date:** 2026-05-27
**Status:** Approved
**Backlog entry:** `docs/backlog/improvements.md#IMPROVEMENT-030`

---

## Problem Summary

The AutoMarket has three interconnected problems:

### 1. Plasma buy orders are an unbounded NIC faucet

When a player sells plasma to an AutoMarket buy order, `Market.FiniteVendorBuyOrderTakesTheItem` / `FulfillSellOrderInstantly` calls `PayOutToSeller`, which directly increments the player's wallet balance. There is no vendor wallet being drained. `CentralBank.SubAmount` is called afterward but it is a pure accounting ledger, not a real balance. **Every plasma sale creates new NIC unconditionally.**

Additionally, the buy order quantity equals 100% of plasma gathered in the past 7 days (`cdp.gathered` from `fn_CalculateDynamicPlasmaPrices`). This is procyclical: more farming → larger buy orders → more NIC created per refresh cycle. There is no daily spending limit.

The price mechanism (`MIN + (MAX–MIN) × (1 – sold/gathered)`) can compress per-unit income over time but does not reduce total NIC injection — the bot still offers to buy the full gathered quantity at the compressed price.

### 2. Raw material prices are backwards and static

`recalculate_raw_material_prices` distributes total plasma NIC proportionally across gathered resource volumes:

```
price_i = total_plasma_nic × (qty_i / total_all_resources)
```

This means **more supply → higher price**, which is the opposite of supply/demand. Common, easy-to-gather materials get relatively high prices; rare materials gathered in small volumes get low prices.

The static `raw_material_prices` table acts as both fallback and clamp anchor. It requires manual maintenance and does not reflect zone risk (alpha materials priced the same as PvP-zone materials).

### 3. Performance and thread-safety concerns

`MarketAutoOrdersManager.Update(time)` fires timer callbacks synchronously on the process loop. `usp_RefreshAutoMarketOrders` uses four SQL cursors (alpha, beta, gamma plasma, raw materials) that execute row-by-row. These are blocking, potentially long-running DB operations on the main process thread. The gather recording call sites in modules also make synchronous DB calls outside the zone transaction but still from the zone processing path.

---

## Solution Design

### Part A — NIC Injection Control

#### A1. `automarket_config` table

New single-row-per-param config table replacing all hardcoded constants:

```sql
CREATE TABLE automarket_config (
    param_name  VARCHAR(100) NOT NULL PRIMARY KEY,
    param_value FLOAT        NOT NULL
);

INSERT INTO automarket_config VALUES
    ('plasma_anchor_fraction',     0.15),  -- fraction of alpha plasma price = raw mat floor
    ('plasma_buy_qty_fraction',    0.60),  -- buy 60% of gathered, not 100%
    ('daily_plasma_budget_nic',    500000),-- max NIC paid out for plasma per calendar day
    ('resource_ds_ratio_min',      0.25),  -- supply/demand ratio floor clamp
    ('resource_ds_ratio_max',      4.0);   -- supply/demand ratio ceiling clamp
```

#### A2. `usp_RefreshAutoMarketOrders` — plasma buy order changes

Before the alpha/beta/gamma cursor blocks:

```sql
DECLARE @buy_qty_fraction  FLOAT = (SELECT param_value FROM automarket_config WHERE param_name = 'plasma_buy_qty_fraction');
DECLARE @daily_budget      FLOAT = (SELECT param_value FROM automarket_config WHERE param_name = 'daily_plasma_budget_nic');
DECLARE @today_spent       FLOAT = ISNULL((SELECT SUM(income) FROM plasma_sold WHERE sold_on = CAST(GETUTCDATE() AS DATE)), 0);
DECLARE @remaining_budget  FLOAT = @daily_budget - @today_spent;
```

For each cursor row, the quantity inserted becomes:

```sql
-- Adjusted quantity: fraction of gathered, capped by remaining budget
DECLARE @adjusted_qty BIGINT = CAST(@quantity * @buy_qty_fraction AS BIGINT);
DECLARE @budget_qty   BIGINT = CASE WHEN @unit_price > 0
                                    THEN CAST(@remaining_budget / @unit_price AS BIGINT)
                                    ELSE 0 END;
SET @quantity = CASE WHEN @adjusted_qty < @budget_qty
                     THEN @adjusted_qty ELSE @budget_qty END;
-- Skip insert if budget exhausted
IF @quantity <= 0 CONTINUE;
```

#### A3. `MarketAutoOrdersManager.cs` — refresh interval

Change `RecalculatePricesAndRenewOrders` timer from `TimeSpan.FromDays(3)` to `TimeSpan.FromDays(1)`. Prices now respond within 24 hours.

---

### Part B — Zone-Aware Gather Tracking

#### B1. Schema: add `is_pvp` column

```sql
ALTER TABLE resources_gathered_daily
    ADD is_pvp BIT NOT NULL DEFAULT 0;

ALTER TABLE resources_gathered
    ADD is_pvp BIT NOT NULL DEFAULT 0;
```

The existing PK/unique index on `resources_gathered` must be updated to include `is_pvp` if one exists; verify before applying.

#### B2. `sp_RecordResourceGathered` — add `@is_pvp` parameter

```sql
ALTER PROCEDURE sp_RecordResourceGathered
    @gathered_on   DATE,
    @resource_name VARCHAR(100),
    @quantity      BIGINT,
    @is_pvp        BIT = 0        -- default 0 = PvE (backward-compatible)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO resources_gathered_daily (gathered_on, resource_name, quantity, is_pvp)
    VALUES (@gathered_on, @resource_name, @quantity, @is_pvp);
END;
```

#### B3. `consolidate_statistics` — preserve `is_pvp` in merge key

```sql
-- In the resources block, change GROUP BY and MERGE ON:
GROUP BY gathered_on, resource_name, is_pvp

ON  target.gathered_on   = source.gathered_on
AND target.resource_name = source.resource_name
AND target.is_pvp        = source.is_pvp

-- INSERT must include is_pvp:
INSERT (gathered_on, resource_name, quantity, is_pvp)
VALUES (source.gathered_on, source.resource_name, source.total_quantity, source.is_pvp)
```

#### B4. C# call sites — pass zone PvP flag

`ZoneConfiguration.Protected == true` → alpha (PvE), `Protected == false` → beta/gamma (PvP).

Five call sites need updating:

| File | Line | Change |
|---|---|---|
| `DrillerModule.cs` | 210 | Add `.SetParameter("@is_pvp", !zone.Configuration.Protected)` |
| `HarvesterModule.cs` | 160 | Same |
| `LargeDrillerModule.cs` | 131 | Same |
| `LargeHarvesterModule.cs` | 102 | Same |
| `LootContainer.cs` | 637 | Verify zone context available; pass flag or default to `false` |

Verify the `zone` variable is in scope at each call site before the patch. The stored proc defaults `@is_pvp = 0`, so any call site that cannot determine zone type can simply omit the parameter.

---

### Part C — Dynamic Risk-Aware Raw Material Pricing

#### C1. Revise `recalculate_raw_material_prices`

Replace the current proportional distribution formula with a supply/demand + PvP-risk formula anchored to live plasma prices. Remove the `raw_material_prices` clamp.

**New formula (per resource, over past 7 days):**

```
pve_qty          = SUM(quantity WHERE is_pvp = 0)
pvp_qty          = SUM(quantity WHERE is_pvp = 1)
total_qty        = pve_qty + pvp_qty
supply_daily_avg = total_qty / 7.0

demand           = SUM(v_required_raw_materials.total_quantity) / 7.0   -- daily average demand from AutoMarket

ds_ratio         = CLAMP(ds_ratio_min, ds_ratio_max, demand / NULLIF(supply_daily_avg, 0))
                   -- no gather data → NULL / NULL → NULL → clamped to ds_ratio_max (max scarcity)

pvp_ratio        = CAST(pvp_qty AS FLOAT) / NULLIF(total_qty, 0)
risk             = 1.0 + ISNULL(pvp_ratio, 1.0)
                   -- no gather data → pvp_ratio NULL → risk = 2.0 (max, assume dangerous)

plasma_anchor    = fn_CalculateDynamicPlasmaPrices(1).dynamic_price
                   × automarket_config['plasma_anchor_fraction']

price            = ROUND(plasma_anchor × ds_ratio × risk, 2)
```

**Price range:** `anchor × 0.25 × 1.0` to `anchor × 4.0 × 2.0` = 0.25×–8× of anchor.

**Reference examples** (alpha plasma at 75 NIC, anchor = 11.25 NIC):

| Material | daily supply | daily demand | ds_ratio | PvP% | risk | Price |
|---|---|---|---|---|---|---|
| Common alpha ore, plentiful | 3× demand | — | 0.33 | 0% | 1.0 | ~3.7 NIC |
| Mixed beta ore, balanced | equal | — | 1.0 | 60% | 1.6 | ~18 NIC |
| PvP gamma ore, scarce | 0.25× | — | 4.0 | 100% | 2.0 | ~90 NIC |
| Never gathered | zero | any | 4.0 (max) | unknown | 2.0 (max) | ~90 NIC |

**Fallback for materials with zero gather history**: the formula naturally handles this — `supply_daily_avg = 0` causes `ds_ratio` to hit the ceiling (4.0), and `pvp_ratio` is NULL → `risk = 2.0`. This correctly signals "nobody is gathering this → scarce and risky."

#### C2. Update `v_all_production_costs` — remove `raw_material_prices` fallback

The current view uses:
```sql
ISNULL(mp.unit_price, base.price_nic)  -- base = raw_material_prices
```

Once `recalculate_raw_material_prices` always produces a price (including for ungathered materials), change to:
```sql
ISNULL(mp.unit_price, <fallback_formula>)
```

Where `<fallback_formula>` is an inline computation of `plasma_anchor × 4.0 × 2.0` for materials completely absent from `resource_market_prices`. This makes the view self-contained.

The `raw_material_prices` table rows are left intact as historical reference but no longer read by any active query path.

---

### Part D — Performance and Thread-Safety Refactoring

#### D1. Analysis scope

During implementation, evaluate the following before writing any fixes:

1. **`MarketAutoOrdersManager.Update(time)` on the process loop** — determine which thread drives this `IProcess`. If it shares the main server process loop, the blocking `ConsolidateStatistics` (every 15 min) and `RecalculatePricesAndRenewOrders` (now daily) will stall that loop for the duration of the DB calls. Measure or estimate DB operation duration and assess whether offloading to `Task.Run` is warranted. If yes, follow existing async patterns in the codebase (do not use fire-and-forget; capture exceptions).

2. **Cursor-based plasma buy order insertion** — `usp_RefreshAutoMarketOrders` uses four SQL cursors that execute row-by-row. The alpha/beta/gamma plasma sections could be replaced with set-based `INSERT ... SELECT` joining `fn_CalculateDynamicPlasmaPrices` results directly to the Markets CTE and vendor table — eliminating the cursor loop entirely. The raw materials section can similarly become a single set-based INSERT. Evaluate and rewrite as set-based if the cursor approach is confirmed as a performance bottleneck.

3. **`resources_gathered_daily` insert frequency** — `sp_RecordResourceGathered` is called per-gather event from five module types. On active servers these are frequent zone-thread calls. Verify the `READPAST` hint in `consolidate_statistics` is sufficient to prevent lock contention between concurrent inserts and the 15-minute merge. No code change required if contention is absent; note the finding regardless.

4. **DELETE-all + INSERT-all pattern in `usp_RefreshAutoMarketOrders`** — every refresh deletes all `isAutoOrder = 1` rows and re-inserts. This causes index churn. Evaluate whether a MERGE-based approach (insert new, update changed, delete removed) would reduce churn. Note: on a small server the table is unlikely to be large, so this may be low priority.

#### D2. Refactoring rules

- Preserve all existing public contracts (market order IDs, packet formats, vendor behavior visible to clients).
- Do not introduce new static service locators.
- Any async changes must follow patterns used in `EventListenerService` or similar — no unguarded `Task.Run` without exception logging.
- Cursor → set-based SQL rewrites must produce identical observable results: same orders placed, same prices, same quantities.

---

## Schema Change Summary

| Object | Change type | Detail |
|---|---|---|
| `automarket_config` | New table | Config params replacing hardcoded constants |
| `resources_gathered_daily` | Alter | Add `is_pvp BIT NOT NULL DEFAULT 0` |
| `resources_gathered` | Alter | Add `is_pvp BIT NOT NULL DEFAULT 0`; update unique index to include `is_pvp` |
| `sp_RecordResourceGathered` | Alter | Add `@is_pvp BIT = 0` parameter |
| `consolidate_statistics` | Alter | Include `is_pvp` in GROUP BY and MERGE key |
| `recalculate_raw_material_prices` | Rewrite | New formula; remove `raw_material_prices` dependency |
| `usp_RefreshAutoMarketOrders` | Alter | Budget cap, fractional quantity, set-based if warranted |
| `v_all_production_costs` | Alter | Remove `raw_material_prices` fallback |
| `raw_material_prices` | Deprecated | Left in DB; removed from all active query paths |

---

## Validation Steps

1. Run `usp_RefreshAutoMarketOrders` manually after deploy; confirm auto buy orders appear for plasma on all alpha/beta/gamma markets with quantity ≤ 60% of last-7-day gathered.
2. Confirm `automarket_config` rows are present and readable.
3. Gather a small amount of resources in an alpha zone and a beta zone. Wait for `consolidate_statistics` to run (≤15 min). Verify `resources_gathered` has separate rows for `is_pvp = 0` and `is_pvp = 1` for the same resource.
4. Run `recalculate_raw_material_prices` manually. Verify `resource_market_prices` has entries for all materials in `v_required_raw_materials`. Verify no material has a NULL or zero price.
5. Query `v_all_production_costs`. Verify no row has NULL `production_cost_nic`. Verify PvP-sourced materials have higher prices than equivalent PvE materials when supply/demand ratio is equal.
6. Sell plasma to the AutoMarket until `plasma_sold.income` for today exceeds `daily_plasma_budget_nic`. Verify subsequent buy orders are either absent or have zero quantity.
7. Build passes with no warnings in modified C# files.

---

## Regression Risk

- **Market order visibility to clients**: auto orders are re-inserted on refresh; no client-side IDs are persisted, so no regression.
- **Production cost calculations**: `v_all_production_costs` is used by `usp_RefreshAutoMarketOrders` for sell order prices. Any price change in the view directly affects what the bot charges for items and robots. Verify item sell prices are in a reasonable range after deploy.
- **`consolidate_statistics` key change**: adding `is_pvp` to the merge key means historical rows (pre-deploy) without `is_pvp` will default to `0`. This is correct — all pre-existing gather data is treated as PvE. No data loss.
- **Modules passing `@is_pvp`**: the stored proc parameter defaults to `0`, so any call site missed during the update will silently treat gathers as PvE. Safe but imprecise; verify all five sites.
