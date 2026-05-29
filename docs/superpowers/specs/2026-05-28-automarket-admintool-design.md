# IMPROVEMENT-031: AdminTool AutoMarket Panel — Design Spec

**Date:** 2026-05-28
**Status:** Approved
**Backlog:** IMPROVEMENT-031

---

## Overview

Add a dedicated **AutoMarket** panel to the AdminTool with four tabs: Config, Trade List, Statistics, and Orders. Follows the Seasons/EquipmentSets module folder pattern: namespace-isolated repository and model types, per-tab ViewModels, XAML views, wired into `MainViewModel`.

No server-side changes. "Refresh Now" calls the stored procedures directly from the AdminTool DB connection — the same two calls the server's `MarketAutoOrdersManager` makes.

---

## 1. Structure

### New folder: `src/Perpetuum.AdminTool/AutoMarket/`

| File | Purpose |
|---|---|
| `AutoMarketRepository.cs` | All DB queries for the panel |
| `AutoMarketConfigRow.cs` | Row model: `param_name`, `param_value`, `Label`, `Description`, `OriginalValue` |
| `AutoMarketTradeListRow.cs` | Row model: `DefinitionName`, `Amount`, `DisplayName`, `OriginalAmount` |
| `AutoMarketRawMaterialRow.cs` | Read-only derived row: `RawMaterialName`, `TotalQuantity` |
| `AutoMarketNicFlowRow.cs` | Statistics row: period, plasma_in, rawmat_out, net, budget_pct |
| `AutoMarketPricingTraceRow.cs` | Statistics row: resource, anchor, sd_ratio, risk_mult, computed_price, stored_price |
| `AutoMarketGatherRow.cs` | Statistics row: resource, pve_qty, pvp_qty, total_qty, pvp_pct |
| `AutoMarketOrderRow.cs` | Orders row: display_name, order_type, price, amount, market_name, category |

### New ViewModels: `src/Perpetuum.AdminTool/ViewModels/`

| File | Purpose |
|---|---|
| `AutoMarketViewModel.cs` | Root VM — owns tab VMs, `RefreshNowCommand` |
| `AutoMarketConfigViewModel.cs` | Tab 1 — editable config grid |
| `AutoMarketTradeListViewModel.cs` | Tab 2 — editable trade list + derived materials sub-panel |
| `AutoMarketStatisticsViewModel.cs` | Tab 3 — read-only NIC flow, pricing trace, gather breakdown |
| `AutoMarketOrdersViewModel.cs` | Tab 4 — read-only live order snapshot with filters |

### New Views: `src/Perpetuum.AdminTool/Views/`

| File | Purpose |
|---|---|
| `AutoMarketView.xaml` | Tab control shell |
| `AutoMarketConfigView.xaml` | Config grid |
| `AutoMarketTradeListView.xaml` | Trade list grid + raw materials sub-panel |
| `AutoMarketStatisticsView.xaml` | Three statistics panels |
| `AutoMarketOrdersView.xaml` | Orders grid with filter controls |

### MainViewModel wiring

```csharp
public AutoMarketViewModel AutoMarket { get; }

// In constructor:
AutoMarket = new AutoMarketViewModel(
    new AutoMarketRepository(store.Settings.Connection),
    session.Changes,
    session.Lookups,
    Translations);
```

---

## 2. Tab 1 — Config

### Data source
`automarket_config` — key-value store (`param_name VARCHAR(100)`, `param_value FLOAT`).

### AutoMarketConfigRow
```
param_name      string   (read-only key)
param_value     double   (editable)
Label           string   (human-readable, resolved from hardcoded map)
Description     string   (tooltip text)
OriginalValue   double   (set on load; dirty detection)
IsDirty         bool     (param_value != OriginalValue)
```

### Label map (9 params)

| param_name | Label | Description |
|---|---|---|
| `plasma_anchor_fraction` | Plasma Anchor Fraction | Fraction of alpha plasma price used as raw material pricing anchor |
| `plasma_buy_qty_fraction` | Plasma Buy Quantity | Fraction of gathered plasma placed as buy orders |
| `daily_plasma_budget_nic` | Daily Plasma Budget (NIC) | Max NIC spent on plasma buy orders per calendar day |
| `daily_rawmat_budget_nic` | Daily Rawmat Budget (NIC) | Max NIC spent on raw material buy orders per calendar day |
| `resource_ds_ratio_min` | S/D Ratio Min | Lower clamp for supply/demand ratio in pricing formula |
| `resource_ds_ratio_max` | S/D Ratio Max | Upper clamp for supply/demand ratio in pricing formula |
| `product_sell_margin` | Product Sell Margin | Production item sell orders priced at production_cost × this value |
| `raw_mat_sell_multiplier` | Rawmat Sell Multiplier | Raw material sell orders priced at production_cost × this value |
| `product_buyback_margin` | Product Buyback Margin | Buyback buy orders priced at production_cost × this value |

### AutoMarketConfigViewModel
- `LoadAsync()` — queries all `automarket_config` rows, joins to label map, populates `Rows`
- `ObservableCollection<AutoMarketConfigRow> Rows`
- `QueueSaveCommand(AutoMarketConfigRow row)` — generates `UPDATE automarket_config SET param_value = @v WHERE param_name = @k`; pushes to `ChangeQueue`; deduplication key: `automarket_config:{param_name}`

### Refresh Now
Lives on root `AutoMarketViewModel`. Executes:
```sql
EXEC recalculate_raw_material_prices;
EXEC usp_RefreshAutoMarketOrders;
```
Runs as a direct DB operation (`Task.Run` + `SqlConnection`). Loading indicator while running; disabled while in progress. Surfaces errors via `MessageBox`.

---

## 3. Tab 2 — Trade List

### Data sources
- `market_orders_configuration` (`definitionname`, `amount`) — editable grid
- `v_required_raw_materials` (`product`, `raw_material`, `total_quantity`) — read-only derived sub-panel
- `entitydefaults` (via LookupCache) + `TranslationsViewModel` — display names and item picker

### AutoMarketTradeListRow
```
DefinitionName   string   (read-only key)
Amount           int      (editable)
DisplayName      string   (translated name; fallback: DefinitionName)
OriginalAmount   int      (dirty detection)
IsDirty          bool
```

### AutoMarketRawMaterialRow
```
RawMaterialName  string   (raw resource_name string — not an integer ID)
TotalQuantity    long     (SUM across all products in current trade list)
```

### AutoMarketTradeListViewModel
- `ObservableCollection<AutoMarketTradeListRow> Rows`
- `ObservableCollection<AutoMarketRawMaterialRow> DerivedMaterials`
- `LoadAsync()` — loads trade list; resolves display names via `TranslationsViewModel`; loads derived materials from `v_required_raw_materials` grouped by `raw_material`
- `QueueSaveCommand(row)` — `UPDATE market_orders_configuration SET amount = @a WHERE definitionname = @d`; deduplication key: `market_orders_configuration:{definitionname}`
- `RemoveCommand(row)` — `DELETE FROM market_orders_configuration WHERE definitionname = @d`; queued; warns via `MessageBox` if the item appears as a dependency in `v_required_raw_materials`
- `AddItemCommand` — opens item picker dialog

### Item picker dialog (`AutoMarketItemPickerWindow`)
- Search box filtering `entitydefaults` rows from LookupCache
- Shows translated name + definition name
- Filtered to exclude items already in the trade list
- On confirm: queues `INSERT INTO market_orders_configuration (definitionname, amount) VALUES (@d, 1)`; adds row to `Rows` with `Amount = 1`

### Derived materials sub-panel
- Read-only; sits below the trade list grid
- Refreshes after load, add, or remove
- Queries `v_required_raw_materials` filtered to current `definitionname` set
- `resource_name` strings displayed as-is (no integer ID; not in translation store)

---

## 4. Tab 3 — Statistics

Read-only, refresh-on-demand. Three panels.

### Panel A — NIC Flow

**Sources:** `plasma_sold` (sold_on, plasma_type, quantity, income), `rawmat_purchased` (purchased_on, item_definition, quantity, income), `automarket_config` (budget params).

Three period rows: Today / Last 7 Days / All Time.

```
AutoMarketNicFlowRow:
  Period              string   ("Today", "Last 7 Days", "All Time")
  PlasmaIn            long     (SUM(income) from plasma_sold for period)
  RawmatOut           long     (SUM(income) from rawmat_purchased for period)
  NetDelta            long     (PlasmaIn - RawmatOut)
  PlasmaBudgetPct     double?  (Today only: today_plasma_spent / daily_plasma_budget × 100)
  RawmatBudgetPct     double?  (Today only: today_rawmat_spent / daily_rawmat_budget × 100)
```

### Panel B — Pricing Trace

Live-computed from DB, mirroring the formula in `recalculate_raw_material_prices`. For each raw material in `v_required_raw_materials`.

```
AutoMarketPricingTraceRow:
  ResourceName      string
  PlasmaAnchor      double   (fn_CalculateDynamicPlasmaPrices anchor × anchor_fraction)
  SdRatio           double   (CLAMP(daily_demand / supply_daily_avg, ds_min, ds_max); ds_max if no supply)
  RiskMultiplier    double   (1.0 + pvp_fraction; 2.0 if no gather data)
  ComputedPrice     double   (ROUND(PlasmaAnchor × SdRatio × RiskMultiplier, 2))
  StoredPrice       double?  (latest unit_price from resource_market_prices — for comparison)
```

**Queries (run in parallel):**
1. `SELECT TOP 1 dynamic_price FROM fn_CalculateDynamicPlasmaPrices(1) WHERE plasma_type = 'def_common_reactor_plasma'`
2. All params from `automarket_config`
3. Supply: `SELECT resource_name, SUM(CASE WHEN is_pvp=1 THEN quantity ELSE 0 END) AS pvp_qty, SUM(quantity) AS total_qty, SUM(quantity)/7.0 AS supply_daily_avg FROM resources_gathered WHERE gathered_on >= DATEADD(DAY,-7,CAST(GETUTCDATE() AS DATE)) GROUP BY resource_name`
4. Demand: `SELECT raw_material, SUM(total_quantity)/7.0 AS daily_demand FROM v_required_raw_materials GROUP BY raw_material`
5. Materials list: `SELECT DISTINCT raw_material FROM v_required_raw_materials`
6. Stored prices: `SELECT resource_name, unit_price FROM resource_market_prices WHERE calculated_on = (SELECT MAX(calculated_on) FROM resource_market_prices)`

Formula applied in C# — no SP call.

### Panel C — Gather Breakdown

```
AutoMarketGatherRow:
  ResourceName  string
  PveQty        long
  PvpQty        long
  TotalQty      long
  PvpPct        double   (PvpQty / TotalQty × 100; 0 if no data)
```

**Query:** `resources_gathered_daily` last 7 days, grouped by `resource_name`, split by `is_pvp`.

### AutoMarketStatisticsViewModel
- `ObservableCollection<AutoMarketNicFlowRow> NicFlow`
- `ObservableCollection<AutoMarketPricingTraceRow> PricingTrace`
- `ObservableCollection<AutoMarketGatherRow> GatherBreakdown`
- `LoadAsync()` — runs all queries, populates collections
- `RefreshCommand` — re-runs `LoadAsync()`; disabled while loading

---

## 5. Tab 4 — Orders

Read-only live snapshot. Refresh-on-demand.

### Data source
`marketitems WHERE isAutoOrder = 1`, joined to:
- `entitydefaults` for `definitionname` → translated display name
- Entity name lookup via `marketeid` for market/base display name

### AutoMarketOrderRow
```
DisplayName    string   (translated item name; fallback: definitionname)
OrderType      string   ("Buy" / "Sell" / "Buyback")
Price          double
Amount         int
MarketName     string   (translated market/base name; fallback: entity name; fallback: EID string)
Category       string   ("Plasma" / "Raw Material" / "Production Item")
```

### Category derivation (in C#)
- **Plasma:** `itemdefinition IN (3271, 3272, 3273, 3274)`
- **Production Item:** `definitionname` present in `market_orders_configuration`
- **Raw Material:** everything else

### Order type derivation (in C#)
- `isSell = 1` → "Sell"
- `isSell = 0` + Plasma → "Buy"
- `isSell = 0` + Raw Material → "Buy"
- `isSell = 0` + Production Item → "Buyback"

### Market name resolution
`marketitems.marketeid` → query `entities WHERE eid = @marketeid` for `name` → translate via TranslationsViewModel → fallback to entity name → fallback to EID string.

### AutoMarketOrdersViewModel
- `ObservableCollection<AutoMarketOrderRow> AllOrders`
- `ObservableCollection<AutoMarketOrderRow> FilteredOrders` (bound to grid)
- `string? OrderTypeFilter` — null = all; changing re-applies filter
- `string? CategoryFilter` — null = all; changing re-applies filter
- `LoadAsync()` — queries, resolves names, derives category/type
- `RefreshCommand` — re-runs `LoadAsync()`; disabled while loading

---

## 6. ChangeQueue Deduplication

Config and Trade List tabs use deduplication keys per IMPROVEMENT-016 pattern:
- Config: `automarket_config:{param_name}`
- Trade List (update): `market_orders_configuration:{definitionname}`
- Trade List (delete): `market_orders_configuration:DELETE:{definitionname}` — replacing any prior non-destructive change for the same key

Note: `ChangeQueue.Add` currently does not deduplicate. The key scheme is defined here so the implementation knows to implement deduplication in `ChangeQueue` or in the tab VMs (whichever matches IMPROVEMENT-016 when that is implemented).

---

## 7. Constraints

- No new DB tables or server-side changes required
- All data comes from tables and views introduced in IMPROVEMENT-030 and ISSUE-024
- Translations: use existing `TranslationsViewModel` throughout; fallback to internal names — never show raw definition IDs
- `resource_name` strings in pricing/gather panels are not integer IDs and have no translation — display as-is
- Refresh Now must be disabled while a refresh is in progress; surfaces server-side errors to the operator
- Derived materials sub-panel is read-only — no ChangeQueue entries
- Statistics and Orders panels: read-only, no ChangeQueue involvement
