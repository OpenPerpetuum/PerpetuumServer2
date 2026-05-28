# ISSUE-024 — AutoMarket Crafter Viability Design

**Date:** 2026-05-28
**Status:** Approved
**Backlog entry:** `docs/backlog/issues.md#ISSUE-024`

---

## Problem Summary

AutoMarket is positioned as a market maker (best price) rather than a backstop (last resort). This structurally eliminates player crafters from the production economy:

- **Raw material buy orders** at `production_cost × 1.0` → farmers prefer AutoMarket over player crafters
- **Production item sell orders** at `production_cost × 1.0` → crafters cannot undercut AutoMarket
- **Raw material sell orders** at `production_cost × 2.0` → crafters buying from AutoMarket cannot profit
- **Raw material buy orders** uncapped → unbounded NIC injection as AutoMarket absorbs all farming output

The goal is to reposition AutoMarket as a price backstop: the gap between AutoMarket prices and fair value is where player trade operates.

---

## Solution Design

### Part A — Config additions

Four new params in `automarket_config`. All are tunable post-deploy without code changes.

```sql
INSERT INTO automarket_config (param_name, param_value) VALUES
    ('product_sell_margin',     1.2),
    ('raw_mat_sell_multiplier', 1.5),
    ('product_buyback_margin',  0.80),
    ('daily_rawmat_budget_nic', 5000000);
```

| param_name | default | purpose |
|---|---|---|
| `product_sell_margin` | `1.2` | Product sell orders at `cost × 1.2` (was 1.0) |
| `raw_mat_sell_multiplier` | `1.5` | Raw mat sell orders at `cost × 1.5` (was 2.0) |
| `product_buyback_margin` | `0.80` | AutoMarket buys products back at `cost × 0.80` |
| `daily_rawmat_budget_nic` | `5000000` | Max NIC paid for raw material purchases per UTC calendar day |

**Crafter viability with these defaults:**
A crafter sourcing raw materials below 1× market price (e.g., directly from farmers) can sell finished products below AutoMarket's 1.2× price and profit. A crafter buying from AutoMarket at 1.5× raw mat cost will still pay more than AutoMarket's 1.2× product price for fully AutoMarket-sourced production — but the 0.80× buyback floor guarantees an exit price for crafters in thin player markets, making crafting economically rational even in the worst case.

---

### Part B — Schema additions

#### B1. `rawmat_purchased` tracking table

Mirrors `plasma_sold`. Tracks NIC paid for raw material AutoMarket buy order fulfillments, used by `usp_RefreshAutoMarketOrders` to enforce the daily budget cap.

```sql
CREATE TABLE dbo.rawmat_purchased (
    purchased_on    DATE   NOT NULL,
    item_definition INT    NOT NULL,
    quantity        BIGINT NOT NULL,
    income          FLOAT  NOT NULL,
    CONSTRAINT PK_rawmat_purchased PRIMARY KEY (purchased_on, item_definition)
);
```

#### B2. `sp_RecordRawMatPurchased` stored procedure

Upserts into `rawmat_purchased`. Called from `Market.cs` whenever an AutoMarket raw material buy order is fulfilled.

```sql
CREATE PROCEDURE [dbo].[sp_RecordRawMatPurchased]
    @purchased_on  DATE,
    @item_def      INT,
    @quantity      BIGINT,
    @income        FLOAT
AS
BEGIN
    SET NOCOUNT ON;
    MERGE dbo.rawmat_purchased AS target
    USING (SELECT @purchased_on, @item_def, @quantity, @income)
          AS source(purchased_on, item_definition, quantity, income)
    ON  target.purchased_on    = source.purchased_on
    AND target.item_definition = source.item_definition
    WHEN MATCHED THEN
        UPDATE SET
            quantity = target.quantity + source.quantity,
            income   = target.income   + source.income
    WHEN NOT MATCHED THEN
        INSERT (purchased_on, item_definition, quantity, income)
        VALUES (source.purchased_on, source.item_definition, source.quantity, source.income);
END;
GO
```

---

### Part C — `usp_RefreshAutoMarketOrders` changes

Five targeted changes. Existing plasma logic and order structure are untouched.

#### C1. Step 0 — Filter production items out of `automarket_unbought_resources`

Without this fix, the new buyback orders (Step 6) would be captured in `automarket_unbought_resources` on the next refresh cycle, incorrectly inflating raw material purchase quantities for production items.

Change the `automarket_unbought_resources` snapshot from:
```sql
WHERE isAutoOrder = 1 AND isSell = 0
  AND itemdefinition NOT IN (3271, 3272, 3273, 3274)
```
To:
```sql
WHERE mi.isAutoOrder = 1 AND mi.isSell = 0
  AND mi.itemdefinition NOT IN (3271, 3272, 3273, 3274)
  AND NOT EXISTS (
      SELECT 1 FROM market_orders_configuration moc
      INNER JOIN entitydefaults ed2 ON ed2.definitionname = moc.definitionname
      WHERE ed2.definition = mi.itemdefinition
  )
```

This requires aliasing `marketitems` as `mi` in the snapshot query.

#### C2. Step 3 — Product sell price margin

Declare and apply `@product_sell_margin`:

```sql
DECLARE @product_sell_margin FLOAT = (
    SELECT param_value FROM automarket_config WHERE param_name = 'product_sell_margin'
);
```

Change the price column in the Step 3 INSERT from:
```sql
pc.production_cost_nic,
```
To:
```sql
pc.production_cost_nic * @product_sell_margin,
```

#### C3. Step 4 — Raw material buy order daily budget cap

Declare budget variables after the existing plasma budget block:

```sql
DECLARE @daily_rawmat_budget   FLOAT = (
    SELECT param_value FROM automarket_config WHERE param_name = 'daily_rawmat_budget_nic'
);
DECLARE @rawmat_spent  FLOAT = ISNULL(
    (SELECT SUM(income) FROM rawmat_purchased WHERE purchased_on = CAST(GETUTCDATE() AS DATE)),
    0
);
DECLARE @remaining_rawmat_budget FLOAT = @daily_rawmat_budget - @rawmat_spent;
```

Add a budget guard to the Combined CTE's final INSERT:

```sql
-- At the end of the Combined INSERT, add:
WHERE c.total_required_quantity > 0
  AND @remaining_rawmat_budget > 0;
```

This is a binary cap: if the daily budget is exhausted, no new raw material buy orders are posted for the day. The existing `automarket_unbought_resources` carry-forward mechanism means materials not purchased today will increase next-cycle buy quantities automatically.

#### C4. Step 5 — Raw material sell price multiplier

Declare and apply `@raw_mat_sell_multiplier`:

```sql
DECLARE @raw_mat_sell_multiplier FLOAT = (
    SELECT param_value FROM automarket_config WHERE param_name = 'raw_mat_sell_multiplier'
);
```

Change the hardcoded `* 2.0` in the Step 5 price column to `* @raw_mat_sell_multiplier`.

#### C5a. `recalculate_raw_material_prices` cleanup extension

Add one line to the existing 90-day cleanup block at the bottom of `recalculate_raw_material_prices`:

```sql
DELETE FROM rawmat_purchased WHERE purchased_on < DATEADD(DAY, -90, @today);
```

Keeps the tracking table from growing unbounded. No new maintenance path.

#### C5b. Step 6 (new) — Production item buyback orders

Declare and apply `@product_buyback_margin`:

```sql
DECLARE @product_buyback_margin FLOAT = (
    SELECT param_value FROM automarket_config WHERE param_name = 'product_buyback_margin'
);
```

Add a new set-based INSERT after Step 5, using the already-resolved `@marketeid` and `@vendoreid` (TM base):

```sql
-- Step 6: Production item buyback buy orders
INSERT INTO marketitems (
    marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
)
SELECT
    @marketeid,
    ed.definition,
    @vendoreid,
    0,
    0,
    pc.production_cost_nic * @product_buyback_margin,
    moc.amount,
    1,
    1
FROM market_orders_configuration moc
INNER JOIN entitydefaults ed ON moc.definitionname = ed.definitionname
INNER JOIN #prod_costs pc    ON moc.definitionname = pc.product;
```

Quantity = `moc.amount` (same as sell order quantity). No separate config; tune `moc.amount` if needed.

---

### Part D — C# changes in `Market.cs`

Three locations in `FulfillSellOrderInstantly` where plasma fulfillment is recorded (lines ~779, ~804, ~836) each get a paired raw material recording block immediately after the plasma block:

```csharp
if (buyOrder.isVendorItem && itemToSell.ED.CategoryFlags.IsCategory(CategoryFlags.cf_raw_material))
{
    using (TransactionScope scope = Db.CreateTransaction())
    {
        _ = Db.Query()
            .CommandText("exec sp_RecordRawMatPurchased @purchased_on, @item_def, @quantity, @income")
            .SetParameter("@purchased_on", DateTime.UtcNow)
            .SetParameter("@item_def", itemToSell.Definition)
            .SetParameter("@quantity", quantity)
            .SetParameter("@income", buyOrder.price * quantity)
            .ExecuteNonQuery();
        scope.Complete();
    }
}
```

`CategoryFlags.cf_raw_material` naturally excludes production items (crafted components, robots) so buyback order fulfillments are not incorrectly counted as raw material purchases.

No changes to `MarketAutoOrdersManager.cs` — all new order types are managed within the existing single refresh cycle.

---

## Schema Change Summary

| Object | Change type | Detail |
|---|---|---|
| `automarket_config` | Alter (insert rows) | 4 new config params |
| `rawmat_purchased` | New table | Daily raw mat purchase tracking |
| `sp_RecordRawMatPurchased` | New procedure | Upsert into `rawmat_purchased` |
| `usp_RefreshAutoMarketOrders` | Alter | Steps 0, 3, 4, 5 modified; Step 6 added |
| `recalculate_raw_material_prices` | Alter | Add 90-day cleanup of `rawmat_purchased` (Part C, section C5a) |
| `Market.cs` | Alter | 3 raw material fulfillment recording hooks |

---

## Validation Steps

1. Run `usp_RefreshAutoMarketOrders` manually. Confirm product sell orders now price at `production_cost × 1.2` and raw material sell orders at `production_cost × 1.5`.
2. Confirm product buyback buy orders exist on the TM base market (`isSell = 0`, prices at ~0.80× production cost).
3. Sell a raw material item (ore/mineral) to an AutoMarket buy order. Confirm a row appears in `rawmat_purchased` for today.
4. Set `daily_rawmat_budget_nic = 1` in `automarket_config`. Run refresh. Confirm raw material buy orders are absent. Restore to `5000000`.
5. Query `v_all_production_costs` — confirm no NULL `production_cost_nic` values (Step 3 price change flows through this view).
6. Confirm `automarket_unbought_resources` does NOT contain production item definitions after a refresh that includes buyback orders.
7. Build passes with 0 errors in `Market.cs`.

---

## Regression Risk

| Risk | Mitigation |
|---|---|
| Product sell prices suddenly higher by 20% | Expected — alerts players to new economy. Validate range sanity in step 1. |
| Raw mat sell prices lower (2.0× → 1.5×) changes crafting costs | Expected positive change. Production cost view uses `resource_market_prices` not sell order prices, so `v_all_production_costs` is unaffected. |
| Buyback orders inflate `automarket_unbought_resources` next cycle | Mitigated by Step C1 exclusion filter. Verify in validation step 6. |
| `rawmat_purchased` MERGE contention under high throughput | Low risk: fulfillments are sequential within a `TransactionScope`; same pattern used for plasma without issues. |
| Market.cs logic change at wrong fulfillment branch | All 3 plasma-recording branches get mirrored; added immediately after existing plasma blocks to keep structure identical. |
