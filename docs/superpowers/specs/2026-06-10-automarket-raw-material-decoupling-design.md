# IMPROVEMENT-040: AutoMarket Raw Material Decoupling Design

**Date:** 2026-06-10  
**Area:** AutoMarket / Economy / Admin Tool  
**Status:** Approved for implementation  
**Cross-references:** IMPROVEMENT-030 (pricing formula), IMPROVEMENT-031 (AdminTool panel), IMPROVEMENT-035 (player order signal — revisit conditions)

---

## 1. Problem

The AutoMarket identifies raw materials exclusively by recursively exploding the BOM of items in `market_orders_configuration` (the trade list). This creates tight coupling:

- Materials for items outside the trade list receive no market support.
- Newly added craftable items require a manual trade list update before their raw material supply chain becomes active.
- The trade list's role is overloaded — it currently drives both finished product orders and raw material coverage.

---

## 2. Goal

Decouple raw material coverage from the trade list:

- **Raw materials** — enumerated from `entitydefaults` using the `cf_raw_material` category flag bitmask. Coverage is automatic for all qualifying materials.
- **Trade list** — scoped to finished product buy/sell/buyback orders only.

---

## 3. Dependency Inversion

```
Current:
  market_orders_configuration
    → v_required_raw_materials (recursive BOM explosion)
      → recalculate_raw_material_prices (material list + demand signal)
      → usp_RefreshAutoMarketOrders #raw_materials (Steps 4 + 5)
      → v_all_production_costs raw_resources CTE

Proposed:
  entitydefaults (categoryflags & 0x114 = 0x114, enabled=1, hidden=0)
    → #covered_rawmats temp table (materialized once per refresh)
      → usp_RefreshAutoMarketOrders Step 4 (buy orders, weekly-cap sized)
      → usp_RefreshAutoMarketOrders Step 5 (sell orders, flag-gated)
      → recalculate_raw_material_prices materials CTE
      → v_all_production_costs raw_resources CTE

  v_trade_list_raw_material_demand (renamed from v_required_raw_materials, unchanged internally)
    → recalculate_raw_material_prices demand_cte only
    (no longer used by usp_RefreshAutoMarketOrders or v_all_production_costs)

  market_orders_configuration
    → usp_RefreshAutoMarketOrders Step 3 (product sell orders)
    → usp_RefreshAutoMarketOrders Step 6 (product buyback orders)
    → v_trade_list_raw_material_demand (still anchored to trade list)
```

---

## 4. Raw Material Coverage Filter

Category flag bitmask: `cf_raw_material = 0x0000000000000114` (decimal 276).  
The bitmask is hierarchical — subcategories (`cf_organic = 0x10114`, `cf_ore = 0x20114`) have the `cf_raw_material` bits set and are included automatically.

SQL filter:
```sql
WHERE (categoryflags & 276) = 276
  AND enabled = 1
  AND hidden  = 0
```

**Before deploying:** validate the resulting entity list against live `entitydefaults` data to confirm no legacy/unobtainable items slip through.

---

## 5. Pricing Formula — Demand Signal

The existing formula from IMPROVEMENT-030 is preserved:

```
price = plasma_anchor × supply_demand_ratio × pvp_risk_multiplier
```

The `demand_cte` in `recalculate_raw_material_prices` continues to source from `v_trade_list_raw_material_demand` (the renamed view). For materials newly covered by IMPROVEMENT-040 that are not in any trade-listed BOM, `daily_demand` returns NULL → `ISNULL(d.daily_demand, 0) = 0` → `ds_max` scarcity. This is the correct default: a material nobody is currently supplying should be priced at maximum scarcity. As gathering activity begins, supply data accumulates in `resources_gathered` and the ratio normalises naturally.

Recipe-graph demand analysis (considered and deferred): adding full-recipe-graph demand would inflate the demand numerator across all materials, requiring re-calibration of `ds_min`/`ds_max` and the anchor fraction. On a low-population server the signal would be near-noise. Revisit only if supply/demand divergence is observed in the economy report after this improvement ships.

---

## 6. Guardrails

Two independent guardrails coexist:

| Guardrail | Scope | Zero semantics |
|---|---|---|
| `weekly_rawmat_cap_default` / `weekly_cap_override` | Max units AutoMarket will purchase per material per rolling 7-day window | 0 = unlimited quantity |
| `daily_rawmat_budget_nic` | Max NIC spent on raw material buy order fulfillments per UTC calendar day | 0 = unlimited budget |

The daily NIC budget remains the hard injection guardrail. The weekly quantity cap prevents any single material from being exploited for unbounded sell volume regardless of price.

Both guardrails must be independently satisfiable: the buy order quantity for a material is `min(remaining_weekly_qty, floor(remaining_daily_budget / price))`.

---

## 7. Schema Changes

### New table — `automarket_rawmat_overrides`

Per-material exceptions to the global defaults. Only materials needing non-default behaviour get a row.

```sql
CREATE TABLE automarket_rawmat_overrides (
    definitionname      VARCHAR(100)  NOT NULL,
    weekly_cap_override INT           NULL,   -- NULL = use global default; 0 = unlimited
    create_buy_orders   BIT           NOT NULL DEFAULT 1,
    create_sell_orders  BIT           NOT NULL DEFAULT 1,
    CONSTRAINT PK_rawmat_overrides PRIMARY KEY CLUSTERED (definitionname)
);
```

### New table — `automarket_rawmat_weekly_tracking`

Written by the C# market sell handler (MERGE) when an AutoMarket raw material buy order is fulfilled. Cleaned up at 90-day rolling window alongside `resources_gathered`.

```sql
CREATE TABLE automarket_rawmat_weekly_tracking (
    week_start      DATE          NOT NULL,
    definitionname  VARCHAR(100)  NOT NULL,
    qty_purchased   BIGINT        NOT NULL DEFAULT 0,
    CONSTRAINT PK_rawmat_weekly PRIMARY KEY CLUSTERED (week_start, definitionname)
);
```

PK order `(week_start, definitionname)` matches the dominant query: filter by current `week_start`, lookup by `definitionname`.

### `automarket_config` — new row

| param_name | param_value | Description |
|---|---|---|
| `weekly_rawmat_cap_default` | `500000000` | Default weekly buy quantity cap per raw material. 0 = unlimited. |

Labels for existing params updated: `daily_rawmat_budget_nic` gains `(0 = unlimited)` annotation in `AutoMarketLabels.cs`.

### View rename

`v_required_raw_materials` → `v_trade_list_raw_material_demand`  
Internal definition unchanged. Executed via `sp_rename`. After rename, only `recalculate_raw_material_prices` references it.

---

## 8. `v_all_production_costs` — `raw_resources` CTE

Replace:
```sql
FROM (SELECT DISTINCT raw_material FROM v_required_raw_materials) base
```
With:
```sql
FROM (
    SELECT definitionname AS raw_material
    FROM entitydefaults
    WHERE (categoryflags & 276) = 276
      AND enabled = 1
      AND hidden  = 0
) base
```

**Performance:** net improvement — a simple `entitydefaults` scan replaces the recursive CTE traversal. `entitydefaults` is a few thousand rows; the bitwise filter is O(n) with no index change required. The view is materialized into `#prod_costs` at the start of `usp_RefreshAutoMarketOrders`, so the gain compounds across all steps that reference `#prod_costs`.

---

## 9. `recalculate_raw_material_prices` Changes

Replace the `materials` CTE:

```sql
-- Before:
materials AS (
    SELECT DISTINCT raw_material AS resource_name FROM v_required_raw_materials
),

-- After:
materials AS (
    SELECT definitionname AS resource_name
    FROM entitydefaults
    WHERE (categoryflags & 276) = 276
      AND enabled = 1
      AND hidden  = 0
),
```

The `demand_cte` continues referencing `v_trade_list_raw_material_demand` (renamed view). No other changes to the procedure.

**Performance note:** `resource_market_prices` grows proportionally with newly covered materials. Verify an index on `(calculated_on, resource_name)` exists; add if absent. The MERGE runs daily — not a hot path.

---

## 10. `usp_RefreshAutoMarketOrders` Changes

### New temp table — `#covered_rawmats`

Replaces `#raw_materials`. Materialized once after `#prod_costs`, before Step 4. The current `#raw_materials` temp table and its indexes are removed.

```sql
DECLARE @weekly_cap_default BIGINT = (
    SELECT param_value FROM automarket_config WHERE param_name = 'weekly_rawmat_cap_default'
);
DECLARE @week_start DATE = DATEADD(DAY,
    -DATEPART(WEEKDAY, CAST(GETUTCDATE() AS DATE)) + 2,
    CAST(GETUTCDATE() AS DATE));

SELECT
    ed.definition,
    ed.definitionname,
    CASE
        WHEN o.weekly_cap_override IS NOT NULL THEN CAST(o.weekly_cap_override AS BIGINT)
        ELSE @weekly_cap_default
    END AS effective_weekly_cap,
    ISNULL(o.create_buy_orders,  1) AS create_buy_orders,
    ISNULL(o.create_sell_orders, 1) AS create_sell_orders
INTO #covered_rawmats
FROM entitydefaults ed
LEFT JOIN automarket_rawmat_overrides o ON o.definitionname = ed.definitionname
WHERE (ed.categoryflags & 276) = 276
  AND ed.enabled = 1
  AND ed.hidden  = 0;

CREATE INDEX IX_crm_def  ON #covered_rawmats (definition);
CREATE INDEX IX_crm_name ON #covered_rawmats (definitionname);
```

### Step 0 — `automarket_unbought_resources`

The `automarket_unbought_resources` snapshot (current Step 0) is no longer used by Steps 4/5 (buy/sell orders are now cap-driven, not need-driven). Remove both the `DELETE FROM automarket_unbought_resources` and the `INSERT INTO automarket_unbought_resources` blocks. The `automarket_unsold_leftovers` snapshot (also in Step 0) is likewise unused after the rework and should be removed too. Both tables are retained (not dropped) until confirmed unused by any other query or tool — see Section 14.

### Step 4 — Raw material buy orders (reworked)

```sql
-- Weekly purchased qty per material for the current week
SELECT definitionname, ISNULL(SUM(qty_purchased), 0) AS qty_this_week
INTO #weekly_purchased
FROM automarket_rawmat_weekly_tracking
WHERE week_start >= @week_start
GROUP BY definitionname;

CREATE INDEX IX_wp_name ON #weekly_purchased (definitionname);

INSERT INTO marketitems (
    marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
)
SELECT
    @marketeid,
    cr.definition,
    @vendoreid,
    0, 0,
    pc.production_cost_nic,
    CASE
        WHEN @remaining_rawmat_budget <= 0 THEN 0
        WHEN cr.effective_weekly_cap = 0
            THEN CAST(@remaining_rawmat_budget / pc.production_cost_nic AS BIGINT)
        ELSE
            LEAST(
                GREATEST(0, cr.effective_weekly_cap - ISNULL(wp.qty_this_week, 0)),
                CAST(@remaining_rawmat_budget / pc.production_cost_nic AS BIGINT)
            )
    END AS order_qty,
    1, 1
FROM #covered_rawmats cr
INNER JOIN #prod_costs      pc ON pc.product       = cr.definitionname
LEFT  JOIN #weekly_purchased wp ON wp.definitionname = cr.definitionname
WHERE cr.create_buy_orders = 1
  AND pc.production_cost_nic > 0
  AND @remaining_rawmat_budget > 0
  AND (
      cr.effective_weekly_cap = 0
      OR ISNULL(wp.qty_this_week, 0) < cr.effective_weekly_cap
  );
```

### Step 5 — Raw material sell orders (reworked)

```sql
INSERT INTO marketitems (
    marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
)
SELECT
    @marketeid,
    cr.definition,
    @vendoreid,
    0, 1,
    pc.production_cost_nic * @raw_mat_sell_multiplier,
    CASE
        WHEN cr.effective_weekly_cap = 0 THEN 10000000
        ELSE cr.effective_weekly_cap
    END,
    1, 1
FROM #covered_rawmats cr
INNER JOIN #prod_costs pc ON pc.product = cr.definitionname
WHERE cr.create_sell_orders = 1
  AND pc.production_cost_nic > 0;
```

When `effective_weekly_cap = 0` (unlimited), sell order quantity falls back to 10,000,000 — consistent with the current fixed sell quantity.

### Cleanup addition

```sql
DELETE FROM automarket_rawmat_weekly_tracking
WHERE week_start < DATEADD(DAY, -90, @today);
```

Added alongside the existing 90-day cleanup block for `plasma_gathered`, `plasma_sold`, `resources_gathered`, `rawmat_purchased`.

---

## 11. C# — Market Sell Handler

When a player sells to an AutoMarket raw material buy order, after the existing `rawmat_purchased` write, add a MERGE into `automarket_rawmat_weekly_tracking`:

```csharp
var weekStart = GetCurrentWeekStart(); // Monday of current UTC week

Db.Query()
    .CommandText(@"
        MERGE automarket_rawmat_weekly_tracking AS t
        USING (VALUES (@week_start, @defname, @qty))
              AS s (week_start, definitionname, qty_purchased)
        ON t.week_start = s.week_start
           AND t.definitionname = s.definitionname
        WHEN MATCHED     THEN UPDATE SET qty_purchased += s.qty_purchased
        WHEN NOT MATCHED THEN INSERT VALUES (s.week_start, s.definitionname, s.qty_purchased);")
    .AddParameter("@week_start", weekStart)
    .AddParameter("@defname",    itemDefinitionName)
    .AddParameter("@qty",        quantity)
    .ExecuteNonQuery();
```

`GetCurrentWeekStart()` returns Monday of the current UTC week — same logic as `@week_start` in `usp_RefreshAutoMarketOrders`.

This is the only C# change required outside the AdminTool. No hot-path impact — the MERGE runs on raw material sell transactions only, on a table that stays under ~1,000 rows.

---

## 12. AdminTool Changes

### Config tab — `AutoMarketConfigViewModel`

No structural change. The new `weekly_rawmat_cap_default` row appears automatically in the editable grid. Add to `AutoMarketLabels.cs`:

```csharp
["weekly_rawmat_cap_default"] = "Weekly Raw Mat Cap (default, 0 = unlimited)",
// Update existing:
["daily_rawmat_budget_nic"]   = "Daily Raw Mat Budget NIC (0 = unlimited)",
```

### New "Raw Materials" tab

Inserted between Trade List and Statistics in `AutoMarketViewModel`'s tab list.

**New files:**

| File | Purpose |
|---|---|
| `AutoMarket/AutoMarketRawMaterialRow.cs` | Row model |
| `ViewModels/AutoMarketRawMaterialsViewModel.cs` | Tab VM, loads grid, wires ChangeQueue |
| `Views/AutoMarketRawMaterialsView.xaml` + `.cs` | XAML DataGrid |

**Grid columns:**

| Column | Editable |
|---|---|
| Name (translated, fallback to definitionname) | No |
| Current Price | No |
| Effective Cap | No |
| Weekly Cap Override (empty = use default, 0 = unlimited) | Yes |
| Bought This Week | No |
| Buy Orders (checkbox) | Yes |
| Sell Orders (checkbox) | Yes |

All editable columns queue changes via ChangeQueue as a MERGE into `automarket_rawmat_overrides`. If all three editable values for a material are being reset to defaults (cap override NULL, both flags = 1), the queued change generates a `DELETE` to avoid orphaned rows.

**Filter toggle:** "Show overrides only" — hides materials with no row in `automarket_rawmat_overrides`.

**Repository query** (`AutoMarketRepository.GetRawMaterialRows()`):

```sql
DECLARE @week_start DATE = DATEADD(DAY,
    -DATEPART(WEEKDAY, CAST(GETUTCDATE() AS DATE)) + 2,
    CAST(GETUTCDATE() AS DATE));

SELECT
    ed.definitionname,
    COALESCE(t.value, ed.definitionname)                    AS display_name,
    ISNULL(rmp.unit_price, 0)                               AS current_price,
    COALESCE(o.weekly_cap_override, cfg.param_value)        AS effective_cap,
    o.weekly_cap_override,
    ISNULL(o.create_buy_orders,  1)                         AS create_buy_orders,
    ISNULL(o.create_sell_orders, 1)                         AS create_sell_orders,
    ISNULL(wt.qty_purchased, 0)                             AS bought_this_week
FROM entitydefaults ed
LEFT JOIN automarket_rawmat_overrides o
    ON o.definitionname = ed.definitionname
LEFT JOIN (
    SELECT resource_name, unit_price
    FROM resource_market_prices
    WHERE calculated_on = (SELECT MAX(calculated_on) FROM resource_market_prices)
) rmp ON rmp.resource_name = ed.definitionname
LEFT JOIN (
    SELECT definitionname, SUM(qty_purchased) AS qty_purchased
    FROM automarket_rawmat_weekly_tracking
    WHERE week_start >= @week_start
    GROUP BY definitionname
) wt ON wt.definitionname = ed.definitionname
CROSS JOIN (
    SELECT param_value
    FROM automarket_config
    WHERE param_name = 'weekly_rawmat_cap_default'
) cfg
LEFT JOIN entitytranslations t
    ON t.definition = ed.definition AND t.languageID = 1
WHERE (ed.categoryflags & 276) = 276
  AND ed.enabled = 1
  AND ed.hidden  = 0
ORDER BY display_name;
```

### Statistics tab — Pricing Trace

Add **Bought This Week** and **Effective Cap** columns to `AutoMarketPricingTraceRow`. The Raw Materials tab shares the same repository method or the Statistics tab executes a lightweight variant. No structural VM change.

---

## 13. Affected Systems Summary

| System | Change |
|---|---|
| `v_required_raw_materials` | Renamed to `v_trade_list_raw_material_demand` (internal unchanged) |
| `v_all_production_costs` | `raw_resources` CTE switches to `entitydefaults` filter |
| `recalculate_raw_material_prices` | `materials` CTE switches to `entitydefaults` filter |
| `usp_RefreshAutoMarketOrders` | `#raw_materials` removed; `#covered_rawmats` added; Steps 4+5 reworked; cleanup extended |
| `automarket_config` | New `weekly_rawmat_cap_default` row |
| `automarket_rawmat_overrides` | New table |
| `automarket_rawmat_weekly_tracking` | New table |
| C# market sell handler | MERGE into `automarket_rawmat_weekly_tracking` on AutoMarket buy order fulfillment |
| AdminTool Config tab | New label entry |
| AdminTool Raw Materials tab | New tab (VM + View + Row model + repository method) |
| AdminTool Statistics Pricing Trace | Two new columns |

---

## 14. Out of Scope

- Recipe-graph demand signal (deferred — see Section 5)
- IMPROVEMENT-035 (player order signal) — revisit deferral conditions after this ships
- `automarket_unbought_resources` table drop — retain until confirmed unused by other systems

---

## 15. Manual Validation Steps

1. Run migration SQL; verify both new tables exist and `automarket_config` has `weekly_rawmat_cap_default`.
2. Confirm `v_trade_list_raw_material_demand` is accessible and returns the same rows as the old `v_required_raw_materials`.
3. Execute `recalculate_raw_material_prices` manually; verify `resource_market_prices` gains rows for materials not previously in the trade list BOM.
4. Execute `usp_RefreshAutoMarketOrders` manually; verify raw material buy and sell orders appear for the expanded material set; verify materials with `create_buy_orders = 0` have no buy orders.
5. In AdminTool Raw Materials tab: confirm all qualifying materials appear; set an override cap and verify it is reflected in the Effective Cap column after refresh; set a material's buy/sell flags to 0 and re-run the refresh; verify no orders appear for that material.
6. Sell a raw material to an AutoMarket buy order in-game; verify `automarket_rawmat_weekly_tracking` is updated with the correct material and quantity.
7. Set `weekly_rawmat_cap_default = 0` in Config tab; re-run refresh; verify buy orders are placed with unlimited (budget-only) quantity.
8. Set `daily_rawmat_budget_nic = 0` in Config tab; re-run refresh; verify no raw material buy orders are throttled by budget.

---

## 16. Potential Regressions

- **`v_all_production_costs` product cost changes** — switching `raw_resources` to `entitydefaults` filter may produce slightly different production costs for items whose raw material components were not previously in the trade list BOM. Monitor crafting cost data after deploy.
- **AutoMarket NIC injection** — covering more raw materials means more buy orders placed, potentially increasing NIC injection. The `daily_rawmat_budget_nic` cap limits exposure, but monitor the economy report (IMPROVEMENT-039) for the first week after deploy.
- **`automarket_unbought_resources` removal from Step 0** — any external query or admin tool reference to this table should be audited before removing the INSERT.
