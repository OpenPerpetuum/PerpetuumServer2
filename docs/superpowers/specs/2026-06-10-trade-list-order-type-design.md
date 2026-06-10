# IMPROVEMENT-042: AutoMarket Trade List — Per-Item Order Type Control

**Date:** 2026-06-10
**Area:** AutoMarket / Economy / AdminTool
**Status:** Approved

---

## Problem

`market_orders_configuration` drives two distinct order types in `usp_RefreshAutoMarketOrders`:

- **Step 3** — product sell orders (bot sells items to players)
- **Step 6** — product buyback orders (bot buys items back from players)

Both steps currently include every row in the table unconditionally. There is no way to suppress one direction per item without removing the row entirely.

---

## Approach

Add two BIT columns to `market_orders_configuration` — one per order type. This mirrors the `automarket_rawmat_overrides.create_buy_orders` / `create_sell_orders` pattern introduced in IMPROVEMENT-040.

The `None` state (both flags off) is valid: it pauses all orders for an item while keeping its row — and its `amount` config — intact.

---

## DB Migration

File: `docs/db_structure/migrations/IMPROVEMENT-042-trade-list-order-type.sql`

```sql
ALTER TABLE market_orders_configuration
  ADD create_sell_orders    BIT NOT NULL DEFAULT 1,
      create_buyback_orders BIT NOT NULL DEFAULT 1;
```

Default `1` on both columns preserves existing `Both` behaviour for all current trade list entries. No data fixup required.

---

## Stored Procedure (`usp_RefreshAutoMarketOrders`)

Two filter predicates added to existing joins — no structural change:

**Step 3** (product sell orders):
```sql
FROM market_orders_configuration moc
...
WHERE moc.create_sell_orders = 1
```

**Step 6** (product buyback orders):
```sql
FROM market_orders_configuration moc
...
WHERE moc.create_buyback_orders = 1
```

---

## AdminTool (Trade List tab)

### Row model

Add two `bool` properties to the existing trade list row model:

- `CreateSellOrders`
- `CreateBuybackOrders`

Both participate in the existing per-row `Queue Save` flow. A save writes all three fields (`amount`, `create_sell_orders`, `create_buyback_orders`) in one `UPDATE`.

### Repository

Expand `SELECT` to include the two new columns. Row construction reads them. The `INSERT` path for new items omits them — DB defaults apply.

### XAML

Add two `DataGridCheckBoxColumn` columns after `Amount`:

| Column label    | Binding                |
|-----------------|------------------------|
| Sell Orders     | `CreateSellOrders`     |
| Buyback Orders  | `CreateBuybackOrders`  |

Standard `DataGridCheckBoxColumn` — no custom cell template needed.

---

## Affected Files

| File | Change |
|------|--------|
| `docs/db_structure/migrations/IMPROVEMENT-042-trade-list-order-type.sql` | New migration |
| `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql` | Filter predicates on Steps 3 and 6 |
| `src/Perpetuum.AdminTool/AutoMarket/AutoMarketTradeListRow.cs` | Two new bool properties |
| `src/Perpetuum.AdminTool/ViewModels/AutoMarketTradeListViewModel.cs` | Repository query + row mapping |
| `src/Perpetuum.AdminTool/Views/AutoMarketTradeListView.xaml` | Two new DataGridCheckBoxColumn |

---

## Defaults & Compatibility

- Migration default `1` = no behaviour change for existing rows.
- SP changes only add `WHERE` predicates — no existing order logic altered.
- New items added via AdminTool after migration default to both orders active (DB default).

---

## Out of Scope

- Raw material order type control — handled separately via `automarket_rawmat_overrides` (IMPROVEMENT-040).
- Plasma order type control — plasma orders are not sourced from `market_orders_configuration`.
