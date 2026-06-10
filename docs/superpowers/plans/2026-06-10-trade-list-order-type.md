# IMPROVEMENT-042: Trade List Per-Item Order Type Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `create_sell_orders` and `create_buyback_orders` BIT flags to `market_orders_configuration` so operators can suppress either order direction per item from the AdminTool Trade List tab.

**Architecture:** Two BIT columns (default 1) added to `market_orders_configuration`; `usp_RefreshAutoMarketOrders` Steps 3 and 6 each gain a `WHERE` predicate on their respective flag; `AutoMarketTradeListRow` grows two bool properties; the repository query expands; `QueueSave` includes the flags in its UPDATE; the XAML gets two checkbox columns.

**Tech Stack:** SQL Server (migration + stored procedure), C# 12 / CommunityToolkit.Mvvm (row model + VM), WPF XAML (DataGrid template columns).

---

## File Map

| File | Change |
|------|--------|
| `docs/db_structure/migrations/IMPROVEMENT-042-trade-list-order-type.sql` | **Create** — ALTER TABLE + CREATE OR ALTER PROCEDURE |
| `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql` | **Modify** — add WHERE predicates to Steps 3 and 6 |
| `src/Perpetuum.AdminTool/AutoMarket/AutoMarketTradeListRow.cs` | **Modify** — two new bool properties + IsDirty update |
| `src/Perpetuum.AdminTool/AutoMarket/AutoMarketRepository.cs` | **Modify** — expand SELECT + row mapping in LoadTradeListAsync |
| `src/Perpetuum.AdminTool/ViewModels/AutoMarketTradeListViewModel.cs` | **Modify** — QueueSave SQL + AddItem defaults |
| `src/Perpetuum.AdminTool/Views/AutoMarketTradeListView.xaml` | **Modify** — two DataGridTemplateColumn checkbox columns |

---

## Task 1: DB Migration File

**Files:**
- Create: `docs/db_structure/migrations/IMPROVEMENT-042-trade-list-order-type.sql`

- [ ] **Step 1: Create the migration file**

```sql
-- IMPROVEMENT-042: Add per-item order type control to market_orders_configuration.
-- Apply while server is ONLINE (column addition with defaults is non-blocking).
-- Apply BEFORE deploying AdminTool changes.

USE [perpetuumsa];
GO

-- 1. Add columns (idempotent)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.market_orders_configuration')
      AND name = 'create_sell_orders'
)
    ALTER TABLE dbo.market_orders_configuration
        ADD create_sell_orders BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.market_orders_configuration')
      AND name = 'create_buyback_orders'
)
    ALTER TABLE dbo.market_orders_configuration
        ADD create_buyback_orders BIT NOT NULL DEFAULT 1;
GO

-- 2. Update stored procedure (idempotent — CREATE OR ALTER)
CREATE OR ALTER PROCEDURE [dbo].[usp_RefreshAutoMarketOrders]
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        DECLARE @marketeid  BIGINT;
        DECLARE @vendoreid  BIGINT;

        -- Step 1: Remove old auto orders
        DELETE FROM marketitems WHERE isAutoOrder = 1;

        -- Materialise expensive recursive-CTE views once so Steps 3-6 do not re-evaluate them.
        SELECT product, production_cost_nic
        INTO #prod_costs
        FROM v_all_production_costs;

        CREATE INDEX IX_pc_product ON #prod_costs (product);

        -- Week start: Monday of current UTC week (matches recalculate_raw_material_prices formula)
        DECLARE @week_start DATE = DATEADD(DAY, -DATEPART(WEEKDAY, CAST(GETUTCDATE() AS DATE)) + 2, CAST(GETUTCDATE() AS DATE));

        DECLARE @weekly_cap_default BIGINT = (
            SELECT CAST(param_value AS BIGINT) FROM automarket_config WHERE param_name = 'weekly_rawmat_cap_default'
        );

        -- All qualifying raw materials with effective cap and buy/sell flags.
        -- Materialized once; Steps 4 and 5 both read from this table.
        SELECT
            ed.definition,
            ed.definitionname,
            CASE
                WHEN o.weekly_cap_override IS NOT NULL THEN CAST(o.weekly_cap_override AS BIGINT)
                ELSE @weekly_cap_default
            END AS effective_weekly_cap,        -- 0 = unlimited
            ISNULL(o.create_buy_orders,  1) AS create_buy_orders,
            ISNULL(o.create_sell_orders, 1) AS create_sell_orders
        INTO #covered_rawmats
        FROM entitydefaults ed
        LEFT JOIN automarket_rawmat_overrides o ON o.definitionname = ed.definitionname
        WHERE ed.categoryflags IN (0x10114, 0x20114, 0x40114)   -- cf_organic, cf_ore, cf_liquid
          AND ed.enabled = 1
          AND ed.hidden  = 0;

        CREATE INDEX IX_crm_def  ON #covered_rawmats (definition);
        CREATE INDEX IX_crm_name ON #covered_rawmats (definitionname);

        -- Weekly purchases so far for the current week, per material.
        SELECT definitionname, ISNULL(SUM(qty_purchased), 0) AS qty_this_week
        INTO #weekly_purchased
        FROM automarket_rawmat_weekly_tracking
        WHERE week_start >= @week_start
        GROUP BY definitionname;

        CREATE INDEX IX_wp_name ON #weekly_purchased (definitionname);

        -- Budget and config params
        DECLARE @buy_qty_fraction FLOAT = (
            SELECT param_value FROM automarket_config WHERE param_name = 'plasma_buy_qty_fraction'
        );
        DECLARE @daily_budget FLOAT = (
            SELECT param_value FROM automarket_config WHERE param_name = 'daily_plasma_budget_nic'
        );
        DECLARE @today_spent FLOAT = ISNULL(
            (SELECT SUM(income) FROM plasma_sold WHERE sold_on = CAST(GETUTCDATE() AS DATE)),
            0
        );
        DECLARE @remaining_budget FLOAT = @daily_budget - @today_spent;

        DECLARE @daily_rawmat_budget FLOAT = (
            SELECT param_value FROM automarket_config WHERE param_name = 'daily_rawmat_budget_nic'
        );
        DECLARE @rawmat_spent FLOAT = ISNULL(
            (SELECT SUM(income) FROM rawmat_purchased WHERE purchased_on = CAST(GETUTCDATE() AS DATE)),
            0
        );
        DECLARE @remaining_rawmat_budget FLOAT = @daily_rawmat_budget - @rawmat_spent;

        DECLARE @product_sell_margin     FLOAT = (SELECT param_value FROM automarket_config WHERE param_name = 'product_sell_margin');
        DECLARE @raw_mat_sell_multiplier FLOAT = (SELECT param_value FROM automarket_config WHERE param_name = 'raw_mat_sell_multiplier');
        DECLARE @product_buyback_margin  FLOAT = (SELECT param_value FROM automarket_config WHERE param_name = 'product_buyback_margin');

        -- Step 1.1: Alpha plasma buy orders (set-based)
        ;WITH AlphaMarkets AS (
            SELECT e.eid
            FROM dbo.entities e
            JOIN dbo.zoneentities ze ON ze.eid = e.eid
            JOIN dbo.zones z ON z.id = ze.zoneID
            WHERE e.definition IN (
                SELECT definition FROM dbo.getDefinitionByCFString('cf_public_docking_base')
            )
            AND z.terraformable = 0
            AND z.protected = 1
        ),
        Markets AS (
            SELECT eid FROM dbo.entities
            WHERE definition = 10 AND parent IN (SELECT eid FROM AlphaMarkets)
        ),
        AlphaOrders AS (
            SELECT
                m.eid   AS marketeid,
                ed.definition AS itemdefinition,
                v.vendorEID   AS submittereid,
                cdp.dynamic_price AS unit_price,
                CASE
                    WHEN cdp.dynamic_price <= 0 OR @remaining_budget <= 0 THEN 0
                    WHEN CAST(cdp.gathered * @buy_qty_fraction AS BIGINT)
                         <= CAST(@remaining_budget / cdp.dynamic_price AS BIGINT)
                        THEN CAST(cdp.gathered * @buy_qty_fraction AS BIGINT)
                    ELSE CAST(@remaining_budget / cdp.dynamic_price AS BIGINT)
                END AS order_qty
            FROM dbo.fn_CalculateDynamicPlasmaPrices(1) cdp
            JOIN dbo.entitydefaults ed ON cdp.plasma_type = ed.definitionname
            CROSS JOIN Markets m
            JOIN dbo.vendors v ON m.eid = v.marketEID
        )
        INSERT INTO marketitems (
            marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
        )
        SELECT marketeid, itemdefinition, submittereid, 0, 0, unit_price, order_qty, 1, 1
        FROM AlphaOrders
        WHERE order_qty > 0;

        -- Step 1.2: Beta plasma buy orders (set-based)
        ;WITH BetaMarkets AS (
            SELECT e.eid
            FROM dbo.entities e
            JOIN dbo.zoneentities ze ON ze.eid = e.eid
            JOIN dbo.zones z ON z.id = ze.zoneID
            WHERE e.definition IN (
                SELECT definition FROM dbo.getDefinitionByCFString('cf_public_docking_base')
            )
            AND z.terraformable = 0
            AND z.protected = 0
        ),
        Markets AS (
            SELECT eid FROM dbo.entities
            WHERE definition = 10 AND parent IN (SELECT eid FROM BetaMarkets)
        ),
        BetaOrders AS (
            SELECT
                m.eid   AS marketeid,
                ed.definition AS itemdefinition,
                v.vendorEID   AS submittereid,
                cdp.dynamic_price AS unit_price,
                CASE
                    WHEN cdp.dynamic_price <= 0 OR @remaining_budget <= 0 THEN 0
                    WHEN CAST(cdp.gathered * @buy_qty_fraction AS BIGINT)
                         <= CAST(@remaining_budget / cdp.dynamic_price AS BIGINT)
                        THEN CAST(cdp.gathered * @buy_qty_fraction AS BIGINT)
                    ELSE CAST(@remaining_budget / cdp.dynamic_price AS BIGINT)
                END AS order_qty
            FROM dbo.fn_CalculateDynamicPlasmaPrices(2) cdp
            JOIN dbo.entitydefaults ed ON cdp.plasma_type = ed.definitionname
            CROSS JOIN Markets m
            JOIN dbo.vendors v ON m.eid = v.marketEID
        )
        INSERT INTO marketitems (
            marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
        )
        SELECT marketeid, itemdefinition, submittereid, 0, 0, unit_price, order_qty, 1, 1
        FROM BetaOrders
        WHERE order_qty > 0;

        -- Step 1.3: Gamma plasma buy orders (set-based, no vendor EID)
        ;WITH GammaMarkets AS (
            SELECT eid FROM dbo.getLiveGammaDockingBases()
        ),
        Markets AS (
            SELECT eid FROM dbo.entities
            WHERE definition = 10 AND parent IN (SELECT eid FROM GammaMarkets)
        ),
        GammaOrders AS (
            SELECT
                m.eid   AS marketeid,
                ed.definition AS itemdefinition,
                cdp.dynamic_price AS unit_price,
                CASE
                    WHEN cdp.dynamic_price <= 0 OR @remaining_budget <= 0 THEN 0
                    WHEN CAST(cdp.gathered * @buy_qty_fraction AS BIGINT)
                         <= CAST(@remaining_budget / cdp.dynamic_price AS BIGINT)
                        THEN CAST(cdp.gathered * @buy_qty_fraction AS BIGINT)
                    ELSE CAST(@remaining_budget / cdp.dynamic_price AS BIGINT)
                END AS order_qty
            FROM dbo.fn_CalculateDynamicPlasmaPrices(3) cdp
            JOIN dbo.entitydefaults ed ON cdp.plasma_type = ed.definitionname
            CROSS JOIN Markets m
        )
        INSERT INTO marketitems (
            marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
        )
        SELECT marketeid, itemdefinition, 0, 0, 0, unit_price, order_qty, 1, 1
        FROM GammaOrders
        WHERE order_qty > 0;

        -- Step 2: Fetch central market EID and vendor EID
        SELECT @marketeid = eid
        FROM entities
        WHERE ename = 'def_public_market_megacorp_TM_base_tm_pve';

        SELECT @vendoreid = vendorEID
        FROM dbo.vendors
        WHERE marketEID = @marketeid;

        -- Step 3: Product auto sell orders — price at cost * product_sell_margin
        INSERT INTO marketitems (
            marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
        )
        SELECT
            @marketeid,
            ed.definition,
            @vendoreid,
            0,
            1,
            pc.production_cost_nic * @product_sell_margin,
            moc.amount,
            1,
            1
        FROM market_orders_configuration moc
        INNER JOIN entitydefaults ed ON moc.definitionname = ed.definitionname
        INNER JOIN #prod_costs pc    ON moc.definitionname = pc.product
        WHERE moc.create_sell_orders = 1;

        -- Step 4: Raw material buy orders — weekly-cap sized, daily-budget guarded.
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
                WHEN @remaining_rawmat_budget <= 0 OR pc.production_cost_nic <= 0 THEN 0
                WHEN cr.effective_weekly_cap = 0
                    -- Unlimited cap: bounded only by daily NIC budget
                    THEN CAST(@remaining_rawmat_budget / pc.production_cost_nic AS BIGINT)
                WHEN cr.effective_weekly_cap <= ISNULL(wp.qty_this_week, 0) THEN 0
                WHEN (cr.effective_weekly_cap - ISNULL(wp.qty_this_week, 0))
                       <= CAST(@remaining_rawmat_budget / pc.production_cost_nic AS BIGINT)
                    THEN cr.effective_weekly_cap - ISNULL(wp.qty_this_week, 0)
                ELSE CAST(@remaining_rawmat_budget / pc.production_cost_nic AS BIGINT)
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

        -- Step 5: Raw material sell orders — quantity = effective_weekly_cap (0 → fallback 10 000 000).
        INSERT INTO marketitems (
            marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
        )
        SELECT
            @marketeid,
            cr.definition,
            @vendoreid,
            0, 1,
            pc.production_cost_nic * @raw_mat_sell_multiplier,
            CASE WHEN cr.effective_weekly_cap = 0 THEN 10000000 ELSE cr.effective_weekly_cap END,
            1, 1
        FROM #covered_rawmats cr
        INNER JOIN #prod_costs pc ON pc.product = cr.definitionname
        WHERE cr.create_sell_orders = 1
          AND pc.production_cost_nic > 0;

        -- Step 6: Production item buyback buy orders — price at cost * product_buyback_margin
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
        INNER JOIN #prod_costs pc    ON moc.definitionname = pc.product
        WHERE moc.create_buyback_orders = 1;

        -- 90-day rolling cleanup for weekly tracking table
        DECLARE @today_cleanup DATE = CAST(GETUTCDATE() AS DATE);
        DELETE FROM automarket_rawmat_weekly_tracking
        WHERE week_start < DATEADD(DAY, -90, @today_cleanup);

    END TRY
    BEGIN CATCH
        PRINT 'Error in usp_RefreshAutoMarketOrders: ' + ERROR_MESSAGE();
        THROW;
    END CATCH
END;
GO
```

- [ ] **Step 2: Commit the migration file**

```bash
git add docs/db_structure/migrations/IMPROVEMENT-042-trade-list-order-type.sql
git commit -m "IMPROVEMENT-042: migration — add create_sell/buyback_orders to market_orders_configuration"
```

---

## Task 2: Update SP Documentation Snapshot

**Files:**
- Modify: `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql:233` (Step 3 JOIN end)
- Modify: `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql:303` (Step 6 JOIN end)

- [ ] **Step 1: Add WHERE predicate to Step 3 (sell orders)**

In `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql`, find the Step 3 INSERT block (around line 233). Change the final JOIN line:

```sql
        -- Before:
        INNER JOIN #prod_costs pc    ON moc.definitionname = pc.product;

        -- After:
        INNER JOIN #prod_costs pc    ON moc.definitionname = pc.product
        WHERE moc.create_sell_orders = 1;
```

- [ ] **Step 2: Add WHERE predicate to Step 6 (buyback orders)**

In the same file, find the Step 6 INSERT block (around line 303). Change the final JOIN line:

```sql
        -- Before:
        INNER JOIN #prod_costs pc    ON moc.definitionname = pc.product;

        -- After:
        INNER JOIN #prod_costs pc    ON moc.definitionname = pc.product
        WHERE moc.create_buyback_orders = 1;
```

- [ ] **Step 3: Commit the SP doc update**

```bash
git add "docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql"
git commit -m "IMPROVEMENT-042: update SP doc snapshot — filter Steps 3 and 6 by order type flags"
```

---

## Task 3: Update `AutoMarketTradeListRow`

**Files:**
- Modify: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketTradeListRow.cs`

- [ ] **Step 1: Replace the entire file contents**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Perpetuum.AdminTool.AutoMarket
{
    public partial class AutoMarketTradeListRow : ObservableObject
    {
        public string DefinitionName { get; init; } = "";
        public string DisplayName    { get; set;  } = "";
        public int    OriginalAmount { get; set;  }
        public bool   OriginalCreateSellOrders    { get; set; }
        public bool   OriginalCreateBuybackOrders { get; set; }

        [ObservableProperty] private int  _amount;
        [ObservableProperty] private bool _createSellOrders;
        [ObservableProperty] private bool _createBuybackOrders;

        public bool IsDirty =>
            Amount             != OriginalAmount
            || CreateSellOrders    != OriginalCreateSellOrders
            || CreateBuybackOrders != OriginalCreateBuybackOrders;

        partial void OnAmountChanged(int value)             => OnPropertyChanged(nameof(IsDirty));
        partial void OnCreateSellOrdersChanged(bool value)    => OnPropertyChanged(nameof(IsDirty));
        partial void OnCreateBuybackOrdersChanged(bool value) => OnPropertyChanged(nameof(IsDirty));
    }
}
```

- [ ] **Step 2: Build the project to confirm no errors**

```powershell
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/Perpetuum.AdminTool/AutoMarket/AutoMarketTradeListRow.cs
git commit -m "IMPROVEMENT-042: add CreateSellOrders / CreateBuybackOrders to trade list row model"
```

---

## Task 4: Update Repository — `LoadTradeListAsync`

**Files:**
- Modify: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketRepository.cs`

- [ ] **Step 1: Expand the SELECT query and row mapping**

Find `LoadTradeListAsync` in `AutoMarketRepository.cs`. The current query is:

```csharp
cmd.CommandText = "SELECT definitionname, amount FROM market_orders_configuration ORDER BY definitionname";
await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    var name   = reader.GetString(0);
    var amount = reader.GetInt32(1);
    result.Add(new AutoMarketTradeListRow
    {
        DefinitionName = name,
        DisplayName    = name,
        Amount         = amount,
        OriginalAmount = amount,
    });
}
```

Replace it with:

```csharp
cmd.CommandText = "SELECT definitionname, amount, create_sell_orders, create_buyback_orders " +
                  "FROM market_orders_configuration ORDER BY definitionname";
await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    var name      = reader.GetString(0);
    var amount    = reader.GetInt32(1);
    var createSell    = reader.GetBoolean(2);
    var createBuyback = reader.GetBoolean(3);
    result.Add(new AutoMarketTradeListRow
    {
        DefinitionName              = name,
        DisplayName                 = name,
        Amount                      = amount,
        OriginalAmount              = amount,
        CreateSellOrders            = createSell,
        OriginalCreateSellOrders    = createSell,
        CreateBuybackOrders         = createBuyback,
        OriginalCreateBuybackOrders = createBuyback,
    });
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/Perpetuum.AdminTool/AutoMarket/AutoMarketRepository.cs
git commit -m "IMPROVEMENT-042: expand LoadTradeListAsync to read order type flags"
```

---

## Task 5: Update `AutoMarketTradeListViewModel` — QueueSave and AddItem

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/AutoMarketTradeListViewModel.cs`

- [ ] **Step 1: Update `QueueSave` to include new columns in the UPDATE and reset originals**

Find the `QueueSave` method. Replace the `_queue.Add(...)` call and the `row.OriginalAmount` assignment:

```csharp
// Before:
_queue.Add(new RawSqlChange(
    description,
    $"UPDATE market_orders_configuration SET amount = {SqlLiteral.Of(row.Amount)} " +
    $"WHERE definitionname = {SqlLiteral.Of(row.DefinitionName)}"));
row.OriginalAmount = row.Amount;

// After:
_queue.Add(new RawSqlChange(
    description,
    $"UPDATE market_orders_configuration " +
    $"SET amount = {SqlLiteral.Of(row.Amount)}, " +
    $"create_sell_orders = {SqlLiteral.Of(row.CreateSellOrders)}, " +
    $"create_buyback_orders = {SqlLiteral.Of(row.CreateBuybackOrders)} " +
    $"WHERE definitionname = {SqlLiteral.Of(row.DefinitionName)}"));
row.OriginalAmount              = row.Amount;
row.OriginalCreateSellOrders    = row.CreateSellOrders;
row.OriginalCreateBuybackOrders = row.CreateBuybackOrders;
```

- [ ] **Step 2: Update `AddItem` to initialise new flags with defaults**

Find the `Rows.Add(new AutoMarketTradeListRow { ... })` call inside `AddItem`. Add the four new properties:

```csharp
// Before:
Rows.Add(new AutoMarketTradeListRow
{
    DefinitionName = item.DefinitionName,
    DisplayName    = item.DisplayName,
    Amount         = 1,
    OriginalAmount = 1,
});

// After:
Rows.Add(new AutoMarketTradeListRow
{
    DefinitionName              = item.DefinitionName,
    DisplayName                 = item.DisplayName,
    Amount                      = 1,
    OriginalAmount              = 1,
    CreateSellOrders            = true,
    OriginalCreateSellOrders    = true,
    CreateBuybackOrders         = true,
    OriginalCreateBuybackOrders = true,
});
```

- [ ] **Step 3: Build**

```powershell
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Commit**

```bash
git add src/Perpetuum.AdminTool/ViewModels/AutoMarketTradeListViewModel.cs
git commit -m "IMPROVEMENT-042: include order type flags in QueueSave UPDATE and AddItem defaults"
```

---

## Task 6: Update `AutoMarketTradeListView.xaml` — Checkbox Columns

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/AutoMarketTradeListView.xaml`

- [ ] **Step 1: Insert two checkbox columns after the Amount column**

In the Trade List `DataGrid.Columns`, find the Amount column:

```xml
<DataGridTextColumn Header="Amount" Binding="{Binding Amount, UpdateSourceTrigger=LostFocus}" Width="80"/>
```

Insert the two new template columns immediately after it (before the Queue Save template column):

```xml
<DataGridTextColumn Header="Amount" Binding="{Binding Amount, UpdateSourceTrigger=LostFocus}" Width="80"/>
<DataGridTemplateColumn Header="Sell Orders" Width="80">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <CheckBox IsChecked="{Binding CreateSellOrders, UpdateSourceTrigger=PropertyChanged}"
                      HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
<DataGridTemplateColumn Header="Buyback Orders" Width="95">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <CheckBox IsChecked="{Binding CreateBuybackOrders, UpdateSourceTrigger=PropertyChanged}"
                      HorizontalAlignment="Center" VerticalAlignment="Center"/>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

- [ ] **Step 2: Build**

```powershell
dotnet build src/Perpetuum.AdminTool/Perpetuum.AdminTool.csproj -c Release -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 3: Commit**

```bash
git add src/Perpetuum.AdminTool/Views/AutoMarketTradeListView.xaml
git commit -m "IMPROVEMENT-042: add Sell Orders / Buyback Orders checkbox columns to Trade List"
```

---

## Task 7: Apply Migration and Manual Validation

- [ ] **Step 1: Apply the migration to the database**

Run `docs/db_structure/migrations/IMPROVEMENT-042-trade-list-order-type.sql` against the `perpetuumsa` database. The `ALTER TABLE` steps are safe to apply while the server is online (adding a NOT NULL column with a default is non-blocking in SQL Server). The `CREATE OR ALTER PROCEDURE` can also be applied online.

Verify:

```sql
SELECT TOP 5 definitionname, amount, create_sell_orders, create_buyback_orders
FROM market_orders_configuration;
```

Expected: all existing rows show `create_sell_orders = 1`, `create_buyback_orders = 1`.

- [ ] **Step 2: Launch the AdminTool and open the Trade List tab**

- All existing items should show both checkboxes ticked.
- The Amount column still shows the correct values.

- [ ] **Step 3: Test suppressing sell orders for one item**

- Uncheck "Sell Orders" for any item and click "Queue Save".
- Open the ChangeQueue script. Verify it contains:

```sql
UPDATE market_orders_configuration SET amount = <n>, create_sell_orders = 0, create_buyback_orders = 1 WHERE definitionname = N'<item>'
```

- Commit the script. Re-load the tab. Confirm the checkbox is still unchecked.

- [ ] **Step 4: Trigger AutoMarket refresh and verify DB**

Trigger a refresh (via AdminTool Refresh Now or by running `EXEC usp_RefreshAutoMarketOrders` directly). Then verify:

```sql
-- Should return 0 rows for the item you unchecked sell orders for:
SELECT * FROM marketitems
WHERE itemdefinition = (SELECT definition FROM entitydefaults WHERE definitionname = N'<item>')
  AND isSell = 1
  AND isAutoOrder = 1;

-- Should still return a buyback row:
SELECT * FROM marketitems
WHERE itemdefinition = (SELECT definition FROM entitydefaults WHERE definitionname = N'<item>')
  AND isSell = 0
  AND isAutoOrder = 1;
```

- [ ] **Step 5: Test the None state (both unchecked)**

- Uncheck both boxes for a second item, Queue Save, commit, refresh AutoMarket.
- Verify no `isAutoOrder = 1` rows exist in `marketitems` for that item.

- [ ] **Step 6: Verify existing items unaffected**

- Confirm that all items where neither flag was changed still have both sell and buyback orders in `marketitems`.

- [ ] **Step 7: Update backlog**

In `docs/backlog/improvements.md`, mark IMPROVEMENT-042 as `DONE` and add an Implementation Summary section matching the format of adjacent entries.

```bash
git add docs/backlog/improvements.md
git commit -m "IMPROVEMENT-042: mark DONE in backlog"
```
