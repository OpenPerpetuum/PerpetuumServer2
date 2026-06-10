# IMPROVEMENT-040: AutoMarket Raw Material Decoupling — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Decouple raw material coverage from the trade list by enumerating qualifying materials from `entitydefaults` (via the `cf_raw_material` category flag), replace the BOM-explosion-based buy/sell order sizing with a configurable weekly quantity cap per material, and expose both guardrails in the AdminTool.

**Architecture:** A new `#covered_rawmats` temp table (derived from `entitydefaults` + `automarket_rawmat_overrides`) replaces `#raw_materials` as the raw material source in `usp_RefreshAutoMarketOrders`. The renamed view `v_trade_list_raw_material_demand` (formerly `v_required_raw_materials`) is retained only for the demand signal in `recalculate_raw_material_prices`. A new `automarket_rawmat_weekly_tracking` table records fulfilled raw material purchases per week; the market sell handler writes to it alongside the existing `rawmat_purchased` write.

**Tech Stack:** SQL Server (T-SQL), .NET 8, C# 12, WPF/XAML, CommunityToolkit.Mvvm, Microsoft.Data.SqlClient

**Spec:** `docs/superpowers/specs/2026-06-10-automarket-raw-material-decoupling-design.md`

---

## File Map

**Create:**
- `docs/db_structure/migrations/IMPROVEMENT-040-rawmat-decoupling.sql`
- `docs/db_structure/views/v_trade_list_raw_material_demand.sql`
- `docs/db_structure/stored_procedures/dbo.sp_RecordRawMatWeeklyPurchased.StoredProcedure.sql`
- `src/Perpetuum.AdminTool/AutoMarket/AutoMarketCoveredMaterialRow.cs`
- `src/Perpetuum.AdminTool/ViewModels/AutoMarketRawMaterialsViewModel.cs`
- `src/Perpetuum.AdminTool/Views/AutoMarketRawMaterialsView.xaml`
- `src/Perpetuum.AdminTool/Views/AutoMarketRawMaterialsView.xaml.cs`

**Modify:**
- `docs/db_structure/views/v_required_raw_materials.sql` *(delete — replaced by v_trade_list_raw_material_demand.sql)*
- `docs/db_structure/views/v_all_production_costs.sql`
- `docs/db_structure/stored_procedures/dbo.recalculate_raw_material_prices.StoredProcedure.sql`
- `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql`
- `docs/db_structure/database_schema_documentation.md`
- `src/Perpetuum/Services/MarketEngine/Market.cs`
- `src/Perpetuum.AdminTool/AutoMarket/AutoMarketLabels.cs`
- `src/Perpetuum.AdminTool/AutoMarket/AutoMarketPricingTraceRow.cs`
- `src/Perpetuum.AdminTool/AutoMarket/AutoMarketRepository.cs`
- `src/Perpetuum.AdminTool/ViewModels/AutoMarketStatisticsViewModel.cs`
- `src/Perpetuum.AdminTool/ViewModels/AutoMarketViewModel.cs`
- `src/Perpetuum.AdminTool/Views/AutoMarketView.xaml`

---

## Task 1: DB Migration SQL + `sp_RecordRawMatWeeklyPurchased` stored procedure

**Files:**
- Create: `docs/db_structure/migrations/IMPROVEMENT-040-rawmat-decoupling.sql`
- Create: `docs/db_structure/stored_procedures/dbo.sp_RecordRawMatWeeklyPurchased.StoredProcedure.sql`
- Modify: `docs/db_structure/database_schema_documentation.md`

- [ ] **Step 1: Create the migration file**

```sql
-- docs/db_structure/migrations/IMPROVEMENT-040-rawmat-decoupling.sql
-- IMPROVEMENT-040: AutoMarket Raw Material Decoupling
-- Run against perpetuumsa while server is ONLINE (no data migration needed).
-- Apply in order — objects are dependencies of later steps.

USE [perpetuumsa];
GO

--------------------------------------------------------------------
-- 1. New table: per-material AutoMarket overrides
--------------------------------------------------------------------
IF OBJECT_ID('dbo.automarket_rawmat_overrides', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.automarket_rawmat_overrides (
        definitionname      VARCHAR(100)  NOT NULL,
        weekly_cap_override INT           NULL,   -- NULL = use global default; 0 = unlimited
        create_buy_orders   BIT           NOT NULL DEFAULT 1,
        create_sell_orders  BIT           NOT NULL DEFAULT 1,
        CONSTRAINT PK_rawmat_overrides PRIMARY KEY CLUSTERED (definitionname)
    );
    PRINT 'Created automarket_rawmat_overrides';
END
GO

--------------------------------------------------------------------
-- 2. New table: weekly quantity tracking per raw material
--------------------------------------------------------------------
IF OBJECT_ID('dbo.automarket_rawmat_weekly_tracking', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.automarket_rawmat_weekly_tracking (
        week_start      DATE          NOT NULL,
        definitionname  VARCHAR(100)  NOT NULL,
        qty_purchased   BIGINT        NOT NULL DEFAULT 0,
        CONSTRAINT PK_rawmat_weekly PRIMARY KEY CLUSTERED (week_start, definitionname)
    );
    PRINT 'Created automarket_rawmat_weekly_tracking';
END
GO

--------------------------------------------------------------------
-- 3. New automarket_config row: weekly_rawmat_cap_default
--------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM automarket_config WHERE param_name = 'weekly_rawmat_cap_default')
BEGIN
    INSERT INTO automarket_config (param_name, param_value)
    VALUES ('weekly_rawmat_cap_default', 500000000);
    PRINT 'Inserted weekly_rawmat_cap_default into automarket_config';
END
GO

--------------------------------------------------------------------
-- 4. Index on resource_market_prices(calculated_on, resource_name)
--    Required for efficient MERGE in recalculate_raw_material_prices
--    after material list expands.
--------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.resource_market_prices')
      AND name = 'IX_rmp_on_name'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_rmp_on_name
        ON dbo.resource_market_prices (calculated_on, resource_name);
    PRINT 'Created IX_rmp_on_name on resource_market_prices';
END
GO

--------------------------------------------------------------------
-- 5. Rename view: v_required_raw_materials → v_trade_list_raw_material_demand
--------------------------------------------------------------------
IF OBJECT_ID('dbo.v_required_raw_materials', 'V') IS NOT NULL
   AND OBJECT_ID('dbo.v_trade_list_raw_material_demand', 'V') IS NULL
BEGIN
    EXEC sp_rename 'dbo.v_required_raw_materials', 'v_trade_list_raw_material_demand';
    PRINT 'Renamed v_required_raw_materials to v_trade_list_raw_material_demand';
END
GO

--------------------------------------------------------------------
-- 6. New stored procedure: sp_RecordRawMatWeeklyPurchased
--------------------------------------------------------------------
-- (See step 2 for the CREATE OR ALTER body — apply after running this file)
```

- [ ] **Step 2: Create `sp_RecordRawMatWeeklyPurchased` doc file**

```sql
-- docs/db_structure/stored_procedures/dbo.sp_RecordRawMatWeeklyPurchased.StoredProcedure.sql
USE [perpetuumsa]
GO

---- Upsert raw material AutoMarket purchase record for weekly quantity cap tracking.
---- Called by Market.FulfillSellOrderInstantly for every AutoMarket raw material buy
---- order fulfillment — alongside sp_RecordRawMatPurchased.

CREATE OR ALTER PROCEDURE [dbo].[sp_RecordRawMatWeeklyPurchased]
    @week_start     DATE,
    @definitionname VARCHAR(100),
    @quantity       BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    MERGE dbo.automarket_rawmat_weekly_tracking AS target
    USING (SELECT @week_start, @definitionname, @quantity)
          AS source(week_start, definitionname, qty_purchased)
    ON  target.week_start     = source.week_start
    AND target.definitionname = source.definitionname
    WHEN MATCHED THEN
        UPDATE SET qty_purchased = target.qty_purchased + source.qty_purchased
    WHEN NOT MATCHED THEN
        INSERT (week_start, definitionname, qty_purchased)
        VALUES (source.week_start, source.definitionname, source.qty_purchased);
END;
GO
```

Add this SP to the migration file above step 6 comment block — paste the `CREATE OR ALTER PROCEDURE` body there, or execute it separately after the migration.

- [ ] **Step 3: Add this SP to the end of the migration file**

Append to `docs/db_structure/migrations/IMPROVEMENT-040-rawmat-decoupling.sql`:

```sql
CREATE OR ALTER PROCEDURE [dbo].[sp_RecordRawMatWeeklyPurchased]
    @week_start     DATE,
    @definitionname VARCHAR(100),
    @quantity       BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    MERGE dbo.automarket_rawmat_weekly_tracking AS target
    USING (SELECT @week_start, @definitionname, @quantity)
          AS source(week_start, definitionname, qty_purchased)
    ON  target.week_start     = source.week_start
    AND target.definitionname = source.definitionname
    WHEN MATCHED THEN
        UPDATE SET qty_purchased = target.qty_purchased + source.qty_purchased
    WHEN NOT MATCHED THEN
        INSERT (week_start, definitionname, qty_purchased)
        VALUES (source.week_start, source.definitionname, source.qty_purchased);
END;
GO
```

- [ ] **Step 4: Update `database_schema_documentation.md`**

In `docs/db_structure/database_schema_documentation.md`:

1. In the table of contents, add entries (alphabetical order near `automarket_config`):
```markdown
- [automarket_rawmat_overrides](#automarket-rawmat-overrides)
- [automarket_rawmat_weekly_tracking](#automarket-rawmat-weekly-tracking)
```

2. After the `automarket_config` section, add:
```markdown
## automarket_rawmat_overrides

**Schema:** `dbo`

Per-material overrides for AutoMarket raw material coverage. Materials with no row use global defaults from `automarket_config`.

### Columns

| Column | Definition |
|---|---|
| `definitionname` | `varchar(100) [not null, pk]` |
| `weekly_cap_override` | `int [null]` — NULL = use global default; 0 = unlimited |
| `create_buy_orders` | `bit [not null, default: 1]` |
| `create_sell_orders` | `bit [not null, default: 1]` |

---

## automarket_rawmat_weekly_tracking

**Schema:** `dbo`

Tracks units of each raw material purchased via AutoMarket buy orders per week. Written by `sp_RecordRawMatWeeklyPurchased`. Rolled up by `usp_RefreshAutoMarketOrders` to enforce the per-material weekly cap. Cleaned up at 90-day rolling window.

### Columns

| Column | Definition |
|---|---|
| `week_start` | `date [not null, pk]` — Monday of the ISO week |
| `definitionname` | `varchar(100) [not null, pk]` |
| `qty_purchased` | `bigint [not null, default: 0]` |
```

3. In the `automarket_config` seeded rows table, add the new row:
```markdown
| `weekly_rawmat_cap_default` | `500000000` | Default weekly buy quantity cap per raw material. 0 = unlimited. |
```

4. Update the `daily_rawmat_budget_nic` description to add `(0 = unlimited)`:
```markdown
| `daily_rawmat_budget_nic` | `5000000` | Max NIC spent on raw material buy orders per UTC calendar day (0 = unlimited). |
```

5. Find the `v_required_raw_materials` reference in any index/TOC and rename it to `v_trade_list_raw_material_demand`.

- [ ] **Step 5: Commit**

```
git add docs/db_structure/migrations/IMPROVEMENT-040-rawmat-decoupling.sql
git add docs/db_structure/stored_procedures/dbo.sp_RecordRawMatWeeklyPurchased.StoredProcedure.sql
git add docs/db_structure/database_schema_documentation.md
git commit -m "IMPROVEMENT-040: migration SQL, sp_RecordRawMatWeeklyPurchased, schema docs"
```

---

## Task 2: View rename doc + `v_all_production_costs` update

**Files:**
- Create: `docs/db_structure/views/v_trade_list_raw_material_demand.sql`
- Delete: `docs/db_structure/views/v_required_raw_materials.sql`
- Modify: `docs/db_structure/views/v_all_production_costs.sql`

- [ ] **Step 1: Create renamed view doc**

Create `docs/db_structure/views/v_trade_list_raw_material_demand.sql` with contents identical to the current `docs/db_structure/views/v_required_raw_materials.sql`, but update the object name in the header comment and `CREATE OR ALTER VIEW` statement:

```sql
/****** Object:  View [dbo].[v_trade_list_raw_material_demand]    Script Date: 10.06.2026 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Returns the raw materials (and quantities) required to fulfil the AutoMarket trade list.
-- Used exclusively as a demand signal in recalculate_raw_material_prices.
-- Material ENUMERATION is now driven by entitydefaults (cf_raw_material flag) — not this view.
-- prod_data inlines production_data to avoid view-nesting-level accumulation inside the recursive member.
CREATE OR ALTER VIEW [dbo].[v_trade_list_raw_material_demand] AS
    WITH prod_data AS (
        SELECT
            ed.definitionname  AS product,
            ced.definitionname AS components,
            c.componentamount  AS amount
        FROM dbo.components c
        INNER JOIN dbo.entitydefaults ed  ON c.definition          = ed.definition
        INNER JOIN dbo.entitydefaults ced ON c.componentdefinition = ced.definition
        WHERE ed.purchasable = 1 AND ed.enabled = 1 AND ed.hidden = 0
    ),
    RecursiveBreakdown AS (
        -- Base case: direct components
        SELECT
            moc.definitionname AS product,
            pd.components AS component,
            SUM(CAST(ROUND(pd.amount * 2.1, 0) AS BIGINT)) AS total_amount
        FROM dbo.market_orders_configuration moc
        JOIN prod_data pd ON moc.definitionname = pd.product
        GROUP BY moc.definitionname, pd.components

        UNION ALL

        -- Recursive case: break down intermediate components
        SELECT
            rb.product,
            pd.components AS component,
            rb.total_amount * CAST(ROUND(pd.amount * 2.1, 0) AS BIGINT) AS total_amount
        FROM RecursiveBreakdown rb
        JOIN prod_data pd ON rb.component = pd.product
    )

    -- Final aggregation: only raw materials (not further craftable)
    SELECT
        rb.product as product,
        rb.component AS raw_material,
        SUM(rb.total_amount) AS total_quantity
    FROM RecursiveBreakdown rb
    LEFT JOIN prod_data pd ON rb.component = pd.product
    WHERE pd.product IS NULL
    GROUP BY rb.product, rb.component;
GO
```

- [ ] **Step 2: Delete the old view doc**

```
git rm docs/db_structure/views/v_required_raw_materials.sql
```

- [ ] **Step 3: Update `v_all_production_costs` — `raw_resources` CTE**

In `docs/db_structure/views/v_all_production_costs.sql`, replace the `raw_resources` CTE:

Old:
```sql
raw_resources AS (
    SELECT
        base.raw_material AS product,
        ISNULL(mp.unit_price, msp.price) AS production_cost_nic
    FROM (SELECT DISTINCT raw_material FROM v_required_raw_materials) base
    LEFT JOIN latest_market_prices mp
        ON base.raw_material COLLATE DATABASE_DEFAULT = mp.resource_name COLLATE DATABASE_DEFAULT
    CROSS JOIN max_scarcity_price msp
),
```

New:
```sql
raw_resources AS (
    SELECT
        base.definitionname AS product,
        ISNULL(mp.unit_price, msp.price) AS production_cost_nic
    FROM (
        SELECT definitionname
        FROM dbo.entitydefaults
        WHERE (categoryflags & 276) = 276   -- cf_raw_material bitmask
          AND enabled = 1
          AND hidden  = 0
    ) base
    LEFT JOIN latest_market_prices mp
        ON base.definitionname COLLATE DATABASE_DEFAULT = mp.resource_name COLLATE DATABASE_DEFAULT
    CROSS JOIN max_scarcity_price msp
),
```

Also update the script date in the header comment to `10.06.2026`.

- [ ] **Step 4: Apply view changes to the database**

Run in SQL Server Management Studio (or equivalent) against perpetuumsa:

```sql
-- Apply renamed view
EXEC sp_rename 'dbo.v_required_raw_materials', 'v_trade_list_raw_material_demand';
-- (skip if already applied in Task 1 migration)

-- Apply updated v_all_production_costs (copy full CREATE OR ALTER VIEW from the updated doc file)
```

Expected: no errors. Run `SELECT TOP 5 * FROM v_trade_list_raw_material_demand` and `SELECT TOP 5 * FROM v_all_production_costs` to verify both return rows.

- [ ] **Step 5: Commit**

```
git add docs/db_structure/views/v_trade_list_raw_material_demand.sql
git add docs/db_structure/views/v_all_production_costs.sql
git commit -m "IMPROVEMENT-040: rename v_required_raw_materials doc, update v_all_production_costs raw_resources CTE"
```

---

## Task 3: Update `recalculate_raw_material_prices`

**Files:**
- Modify: `docs/db_structure/stored_procedures/dbo.recalculate_raw_material_prices.StoredProcedure.sql`

- [ ] **Step 1: Replace the `materials` CTE and update `demand_cte` view reference**

In `docs/db_structure/stored_procedures/dbo.recalculate_raw_material_prices.StoredProcedure.sql`, replace the `materials` and `demand_cte` CTEs inside the `WITH` block:

Old:
```sql
        demand_cte   AS (
                SELECT raw_material,   SUM(total_quantity) / 7.0   AS daily_demand
                FROM   v_required_raw_materials
                GROUP  BY raw_material
        ) ,
        materials    AS (
                SELECT DISTINCT raw_material AS resource_name
                FROM   v_required_raw_materials
        ) ,
```

New:
```sql
        demand_cte   AS (
                SELECT raw_material,   SUM(total_quantity) / 7.0   AS daily_demand
                FROM   v_trade_list_raw_material_demand
                GROUP  BY raw_material
        ) ,
        -- Material enumeration is now driven by entitydefaults (cf_raw_material = 0x114).
        -- New materials with no demand data default to ds_max scarcity price (ISNULL branch below).
        materials    AS (
                SELECT definitionname AS resource_name
                FROM   dbo.entitydefaults
                WHERE  (categoryflags & 276) = 276
                AND    enabled = 1
                AND    hidden  = 0
        ) ,
```

Also update the script date in the header comment to `10.06.2026`.

- [ ] **Step 2: Apply to the database**

Run in SSMS:

```sql
-- Copy full CREATE OR ALTER PROCEDURE body from updated doc file and execute.
```

Expected: procedure compiles without error. Run:
```sql
EXEC recalculate_raw_material_prices;
SELECT COUNT(*) FROM resource_market_prices
WHERE calculated_on = (SELECT MAX(calculated_on) FROM resource_market_prices);
```

Expected: row count increases compared to before (now includes all cf_raw_material items, not just trade-list BOM materials).

- [ ] **Step 3: Commit**

```
git add docs/db_structure/stored_procedures/dbo.recalculate_raw_material_prices.StoredProcedure.sql
git commit -m "IMPROVEMENT-040: recalculate_raw_material_prices uses entitydefaults for material enumeration"
```

---

## Task 4: Rework `usp_RefreshAutoMarketOrders`

**Files:**
- Modify: `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql`

- [ ] **Step 1: Remove Step 0 snapshot blocks**

In `usp_RefreshAutoMarketOrders`, delete these blocks entirely (Steps 4 and 5 are now cap-driven, not need-driven):

```sql
        -- Step 0: Snapshot unsold and unbought items
        DELETE FROM [automarket_unsold_leftovers];
        DELETE FROM [automarket_unbought_resources];

        INSERT INTO [automarket_unsold_leftovers] (itemdefinition, quantity)
        SELECT itemdefinition, SUM(CAST(quantity AS BIGINT))
        FROM marketitems
        WHERE isAutoOrder = 1 AND isSell = 1
        GROUP BY itemdefinition;

        -- Unbought mats: ...
        INSERT INTO automarket_unbought_resources (itemdefinition, quantity)
        ...
        GROUP BY mi.itemdefinition;
```

Keep the Step 1 block (`DELETE FROM marketitems WHERE isAutoOrder = 1;`) and everything that follows.

- [ ] **Step 2: Remove `#raw_materials` temp table, add `#covered_rawmats` and `@week_start`**

After the `#prod_costs` block (which ends with `CREATE INDEX IX_pc_product ON #prod_costs (product);`), replace:

```sql
        SELECT product, raw_material, total_quantity
        INTO #raw_materials
        FROM v_required_raw_materials;

        CREATE INDEX IX_rm_product ON #raw_materials (product);
        CREATE INDEX IX_rm_raw     ON #raw_materials (raw_material);
```

With:

```sql
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
        WHERE (ed.categoryflags & 276) = 276
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
```

- [ ] **Step 3: Replace Step 4 (raw material buy orders)**

Replace the entire Step 4 block:

```sql
        -- Old Step 4: ...NeedProducts...RequiredRaw...Unbought...Combined...
        -- (all of it, from the ';WITH NeedProducts' comment through the final WHERE clause)
```

With:

```sql
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
```

- [ ] **Step 4: Replace Step 5 (raw material sell orders)**

Replace the entire Step 5 block:

```sql
        -- Old Step 5:
        -- INSERT INTO marketitems (...)
        -- SELECT @marketeid, ed.definition, @vendoreid, 0, 1, apc.production_cost_nic * @raw_mat_sell_multiplier,
        --        10000000, 1, 1
        -- FROM #raw_materials rrm
        -- INNER JOIN entitydefaults ed ON rrm.raw_material = ed.definitionname
        -- INNER JOIN #prod_costs apc  ON rrm.raw_material  = apc.product
        -- GROUP BY ed.definition, apc.production_cost_nic;
```

With:

```sql
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
```

- [ ] **Step 5: Add weekly tracking cleanup to the cleanup block**

In `recalculate_raw_material_prices`, the cleanup block already exists. In `usp_RefreshAutoMarketOrders` there is no cleanup block — add it at the end of the procedure, inside the `END TRY` block, before `END TRY`:

```sql
        -- 90-day rolling cleanup for weekly tracking table
        DECLARE @today_cleanup DATE = CAST(GETUTCDATE() AS DATE);
        DELETE FROM automarket_rawmat_weekly_tracking
        WHERE week_start < DATEADD(DAY, -90, @today_cleanup);
```

- [ ] **Step 6: Apply to the database and verify**

Run the full updated `CREATE OR ALTER PROCEDURE [dbo].[usp_RefreshAutoMarketOrders]` from the doc file in SSMS.

Then run:
```sql
EXEC usp_RefreshAutoMarketOrders;

-- Verify raw material orders now cover materials outside the trade list BOM:
SELECT COUNT(*) AS raw_buy_orders
FROM marketitems mi
JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
WHERE mi.isAutoOrder = 1 AND mi.isSell = 0
  AND (ed.categoryflags & 276) = 276;

-- Should be larger than before (all cf_raw_material items, not just trade-listed BOM materials).
```

- [ ] **Step 7: Commit**

```
git add docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql
git commit -m "IMPROVEMENT-040: usp_RefreshAutoMarketOrders — #covered_rawmats replaces #raw_materials, Steps 4+5 reworked"
```

---

## Task 5: C# weekly tracking write in `Market.cs`

**Files:**
- Modify: `src/Perpetuum/Services/MarketEngine/Market.cs`

The three existing `sp_RecordRawMatPurchased` call sites are in `FulfillSellOrderInstantly`. Each is guarded by:
```csharp
if (buyOrder.isVendorItem && itemToSell.ED.CategoryFlags.IsCategory(CategoryFlags.cf_raw_material))
```

Add a private helper method and call it alongside each `sp_RecordRawMatPurchased` block.

- [ ] **Step 1: Add `GetWeekStart` and `RecordWeeklyRawMatPurchase` helpers**

Find the end of the `Market` class (before the closing `}`). Add:

```csharp
private static DateTime GetWeekStart(DateTime utcNow)
{
    // Matches SQL formula: DATEADD(DAY, -DATEPART(WEEKDAY, @today) + 2, @today)
    // with SQL DATEFIRST=7 (Sunday=1, Monday=2, ..., Saturday=7)
    var sqlWeekday = (int)utcNow.DayOfWeek + 1; // Sunday→1, Monday→2, ..., Saturday→7
    return utcNow.Date.AddDays(-sqlWeekday + 2);
}

private void RecordWeeklyRawMatPurchase(string definitionName, int quantity)
{
    using var scope = Db.CreateTransaction();
    Db.Query()
        .CommandText("exec sp_RecordRawMatWeeklyPurchased @week_start, @definitionname, @quantity")
        .SetParameter("@week_start",     GetWeekStart(DateTime.UtcNow))
        .SetParameter("@definitionname", definitionName)
        .SetParameter("@quantity",       quantity)
        .ExecuteNonQuery();
    scope.Complete();
}
```

- [ ] **Step 2: Add call at first `sp_RecordRawMatPurchased` site (case: `buyOrder.quantity < itemToSell.Quantity`)**

Find the block at approximately line 792–806:
```csharp
                    // Log raw material AutoMarket purchase for daily budget tracking
                    if (buyOrder.isVendorItem && itemToSell.ED.CategoryFlags.IsCategory(CategoryFlags.cf_raw_material))
                    {
                        using (TransactionScope scope = Db.CreateTransaction())
                        {
                            _ = Db.Query()
                                .CommandText("exec sp_RecordRawMatPurchased @purchased_on, @item_def, @quantity, @income")
                                .SetParameter("@purchased_on", DateTime.UtcNow)
                                .SetParameter("@item_def", itemToSell.Definition)
                                .SetParameter("@quantity", buyOrder.quantity)
                                .SetParameter("@income", buyOrder.price * buyOrder.quantity)
                                .ExecuteNonQuery();
                            scope.Complete();
                        }
                    }
```

Add immediately after the closing `}` of that `if` block:
```csharp
                    if (buyOrder.isVendorItem && itemToSell.ED.CategoryFlags.IsCategory(CategoryFlags.cf_raw_material))
                        RecordWeeklyRawMatPurchase(itemToSell.ED.Name, buyOrder.quantity);
```

- [ ] **Step 3: Add call at second `sp_RecordRawMatPurchased` site (case: `buyOrder.quantity == itemToSell.Quantity`)**

Find the block at approximately line 832–847 (the second occurrence of the same guard pattern). Add immediately after its closing `}`:
```csharp
                if (buyOrder.isVendorItem && itemToSell.ED.CategoryFlags.IsCategory(CategoryFlags.cf_raw_material))
                    RecordWeeklyRawMatPurchase(itemToSell.ED.Name, quantity);
```

Note: `quantity` here is the local variable set to `buyOrder.quantity` for this branch.

- [ ] **Step 4: Add call at third `sp_RecordRawMatPurchased` site (infinite vendor buy order path)**

Find the block at approximately line 884–898 (the third occurrence). Add immediately after its closing `}`:
```csharp
            if (buyOrder.isVendorItem && itemToSell.ED.CategoryFlags.IsCategory(CategoryFlags.cf_raw_material))
                RecordWeeklyRawMatPurchase(itemToSell.ED.Name, itemToSell.Quantity);
```

- [ ] **Step 5: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors, 0 warnings related to changed files.

- [ ] **Step 6: Commit**

```
git add src/Perpetuum/Services/MarketEngine/Market.cs
git commit -m "IMPROVEMENT-040: record weekly raw mat purchase in Market.FulfillSellOrderInstantly"
```

---

## Task 6: AdminTool row models + labels

**Files:**
- Modify: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketLabels.cs`
- Modify: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketPricingTraceRow.cs`
- Create: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketCoveredMaterialRow.cs`

- [ ] **Step 1: Update `AutoMarketLabels.cs`**

Replace the file content:

```csharp
namespace Perpetuum.AdminTool.AutoMarket
{
    internal static class AutoMarketLabels
    {
        internal record LabelMeta(string Label, string Description);

        internal static readonly IReadOnlyDictionary<string, LabelMeta> Map =
            new Dictionary<string, LabelMeta>
            {
                ["plasma_anchor_fraction"]    = new("Plasma Anchor Fraction",         "Fraction of alpha plasma price used as raw material pricing anchor"),
                ["plasma_buy_qty_fraction"]   = new("Plasma Buy Quantity",             "Fraction of gathered plasma placed as buy orders"),
                ["daily_plasma_budget_nic"]   = new("Daily Plasma Budget (NIC)",       "Max NIC spent on plasma buy orders per calendar day"),
                ["daily_rawmat_budget_nic"]   = new("Daily Rawmat Budget (NIC, 0=∞)",  "Max NIC spent on raw material buy orders per calendar day. 0 = unlimited."),
                ["weekly_rawmat_cap_default"] = new("Weekly Rawmat Cap (default, 0=∞)","Default max units AutoMarket buys per raw material per week. 0 = unlimited."),
                ["resource_ds_ratio_min"]     = new("S/D Ratio Min",                   "Lower clamp for supply/demand ratio in pricing formula"),
                ["resource_ds_ratio_max"]     = new("S/D Ratio Max",                   "Upper clamp for supply/demand ratio in pricing formula"),
                ["product_sell_margin"]       = new("Product Sell Margin",             "Production item sell orders priced at production_cost × this value"),
                ["raw_mat_sell_multiplier"]   = new("Rawmat Sell Multiplier",          "Raw material sell orders priced at production_cost × this value"),
                ["product_buyback_margin"]    = new("Product Buyback Margin",          "Buyback buy orders priced at production_cost × this value"),
            };
    }
}
```

- [ ] **Step 2: Add `BoughtThisWeek` and `EffectiveCap` to `AutoMarketPricingTraceRow`**

Replace the file content:

```csharp
namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketPricingTraceRow
    {
        public string  ResourceName   { get; init; } = "";
        public string  DisplayName    { get; set;  } = "";
        public double  PlasmaAnchor   { get; init; }
        public double  SdRatio        { get; init; }
        public double  RiskMultiplier { get; init; }
        public double  ComputedPrice  { get; init; }
        public double? StoredPrice    { get; init; }
        public long    BoughtThisWeek { get; init; }
        public long    EffectiveCap   { get; init; }
    }
}
```

- [ ] **Step 3: Create `AutoMarketCoveredMaterialRow.cs`**

```csharp
namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketCoveredMaterialRow
    {
        public string  DefinitionName       { get; init; } = "";
        public string  DisplayName          { get; set;  } = "";
        public double  CurrentPrice         { get; init; }
        public long    EffectiveCap         { get; init; }  // BIGINT: COALESCE(override, global default)
        public int?    WeeklyCapOverride    { get; set;  }  // INT NULL: matches DB column type
        public long    BoughtThisWeek       { get; init; }
        public bool    CreateBuyOrders      { get; set;  }
        public bool    CreateSellOrders     { get; set;  }

        // Originals for change detection — need set because QueueSave updates them after dispatch
        public int?    OriginalCapOverride  { get; set;  }
        public bool    OriginalBuyOrders    { get; set;  }
        public bool    OriginalSellOrders   { get; set;  }

        public bool HasOverride =>
            WeeklyCapOverride.HasValue || !CreateBuyOrders || !CreateSellOrders;

        public bool IsAtDefaults =>
            !WeeklyCapOverride.HasValue && CreateBuyOrders && CreateSellOrders;
    }
}
```

- [ ] **Step 4: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/AutoMarket/AutoMarketLabels.cs
git add src/Perpetuum.AdminTool/AutoMarket/AutoMarketPricingTraceRow.cs
git add src/Perpetuum.AdminTool/AutoMarket/AutoMarketCoveredMaterialRow.cs
git commit -m "IMPROVEMENT-040: labels update, PricingTraceRow new columns, AutoMarketCoveredMaterialRow"
```

---

## Task 7: `AutoMarketRepository` updates

**Files:**
- Modify: `src/Perpetuum.AdminTool/AutoMarket/AutoMarketRepository.cs`

Three methods need changes; one new method is added.

- [ ] **Step 1: Update `LoadDerivedMaterialsAsync` — rename view reference**

In `LoadDerivedMaterialsAsync`, replace `v_required_raw_materials` with `v_trade_list_raw_material_demand`:

```csharp
            cmd.CommandText =
                "SELECT raw_material, SUM(total_quantity) " +
                "FROM v_trade_list_raw_material_demand " +
                "GROUP BY raw_material " +
                "ORDER BY raw_material";
```

- [ ] **Step 2: Update `LoadPricingTraceAsync` — materials list + demand view + new columns**

Replace the two references to `v_required_raw_materials` and add two new queries for weekly tracking and effective cap. The full updated `LoadPricingTraceAsync` method:

```csharp
        public async Task<List<AutoMarketPricingTraceRow>> LoadPricingTraceAsync()
        {
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();

            // 1. Alpha plasma anchor price
            double alphaPlasmaPrice = 0;
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT TOP 1 dynamic_price FROM fn_CalculateDynamicPlasmaPrices(1) " +
                    "WHERE plasma_type = 'def_common_reactor_plasma'";
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync()) alphaPlasmaPrice = r.IsDBNull(0) ? 0 : (double)r.GetDecimal(0);
            }

            // 2. Config params
            double anchorFraction = 0.15, dsMin = 0.25, dsMax = 4.0;
            long   weeklyCapDefault = 500_000_000;
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT param_name, param_value FROM automarket_config " +
                    "WHERE param_name IN ('plasma_anchor_fraction','resource_ds_ratio_min','resource_ds_ratio_max','weekly_rawmat_cap_default')";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    switch (r.GetString(0))
                    {
                        case "plasma_anchor_fraction":    anchorFraction   = r.GetDouble(1); break;
                        case "resource_ds_ratio_min":     dsMin            = r.GetDouble(1); break;
                        case "resource_ds_ratio_max":     dsMax            = r.GetDouble(1); break;
                        case "weekly_rawmat_cap_default": weeklyCapDefault = (long)r.GetDouble(1); break;
                    }
                }
            }

            var plasmaAnchor = alphaPlasmaPrice * anchorFraction;

            // 3. Supply data (last 7 days)
            var supply = new Dictionary<string, (double DailyAvg, long PvpQty, long TotalQty)>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT resource_name, " +
                    "  SUM(CASE WHEN is_pvp = 1 THEN quantity ELSE 0 END), " +
                    "  SUM(quantity), " +
                    "  SUM(quantity) / 7.0 " +
                    "FROM resources_gathered " +
                    "WHERE gathered_on >= DATEADD(DAY,-7,CAST(GETUTCDATE() AS DATE)) " +
                    "GROUP BY resource_name";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    supply[r.GetString(0)] = ((double)r.GetDecimal(3), r.GetInt64(1), r.GetInt64(2));
            }

            // 4. Demand data (from trade-list BOM — demand signal only)
            var demand = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT raw_material, SUM(total_quantity) / 7.0 " +
                    "FROM v_trade_list_raw_material_demand GROUP BY raw_material";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) demand[r.GetString(0)] = (double)r.GetDecimal(1);
            }

            // 5. Materials list — all cf_raw_material items (categoryflags & 276 = 276)
            var materials = new List<string>();
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT definitionname FROM entitydefaults " +
                    "WHERE (categoryflags & 276) = 276 AND enabled = 1 AND hidden = 0 " +
                    "ORDER BY definitionname";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) materials.Add(r.GetString(0));
            }

            // 6. Stored prices (latest week)
            var storedPrices = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT resource_name, unit_price FROM resource_market_prices " +
                    "WHERE calculated_on = (SELECT MAX(calculated_on) FROM resource_market_prices)";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) storedPrices[r.GetString(0)] = (double)r.GetDecimal(1);
            }

            // 7. Weekly purchases this week per material
            var weeklyPurchased = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText =
                    "DECLARE @ws DATE = DATEADD(DAY, -DATEPART(WEEKDAY, CAST(GETUTCDATE() AS DATE)) + 2, CAST(GETUTCDATE() AS DATE)); " +
                    "SELECT definitionname, ISNULL(SUM(qty_purchased),0) " +
                    "FROM automarket_rawmat_weekly_tracking WHERE week_start >= @ws " +
                    "GROUP BY definitionname";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) weeklyPurchased[r.GetString(0)] = r.GetInt64(1);
            }

            // 8. Per-material cap overrides
            var capOverrides = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            await using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = "SELECT definitionname, weekly_cap_override FROM automarket_rawmat_overrides WHERE weekly_cap_override IS NOT NULL";
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) capOverrides[r.GetString(0)] = r.GetInt32(1);
            }

            // Compute rows
            var result = new List<AutoMarketPricingTraceRow>();
            foreach (var name in materials)
            {
                var hasSupply = supply.TryGetValue(name, out var sup);
                double supplyDailyAvg = hasSupply ? sup.DailyAvg : 0;
                demand.TryGetValue(name, out var dailyDemand);

                double sdRatio = supplyDailyAvg <= 0
                    ? dsMax
                    : Math.Clamp(dailyDemand / supplyDailyAvg, dsMin, dsMax);

                double pvpFraction = (hasSupply && sup.TotalQty > 0)
                    ? (double)sup.PvpQty / sup.TotalQty
                    : 1.0;

                var riskMultiplier = 1.0 + pvpFraction;
                var computedPrice  = Math.Round(plasmaAnchor * sdRatio * riskMultiplier, 2);
                var effectiveCap   = capOverrides.TryGetValue(name, out var ov) ? ov : weeklyCapDefault;

                result.Add(new AutoMarketPricingTraceRow
                {
                    ResourceName   = name,
                    PlasmaAnchor   = Math.Round(plasmaAnchor, 4),
                    SdRatio        = Math.Round(sdRatio, 4),
                    RiskMultiplier = Math.Round(riskMultiplier, 4),
                    ComputedPrice  = computedPrice,
                    StoredPrice    = storedPrices.TryGetValue(name, out var sp) ? sp : null,
                    BoughtThisWeek = weeklyPurchased.TryGetValue(name, out var bw) ? bw : 0,
                    EffectiveCap   = effectiveCap,
                });
            }
            return result;
        }
```

- [ ] **Step 3: Add `LoadCoveredMaterialsAsync`**

Add after `LoadPricingTraceAsync`:

```csharp
        public async Task<List<AutoMarketCoveredMaterialRow>> LoadCoveredMaterialsAsync()
        {
            var result = new List<AutoMarketCoveredMaterialRow>();
            await using var cn = new SqlConnection(_connection.BuildConnectionString());
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandTimeout = 30;
            cmd.CommandText =
                "DECLARE @ws DATE = DATEADD(DAY, -DATEPART(WEEKDAY, CAST(GETUTCDATE() AS DATE)) + 2, CAST(GETUTCDATE() AS DATE)); " +
                "SELECT " +
                "  ed.definitionname, " +
                "  ISNULL(rmp.unit_price, 0)                                   AS current_price, " +
                "  COALESCE(o.weekly_cap_override, CAST(cfg.param_value AS BIGINT)) AS effective_cap, " +
                "  o.weekly_cap_override, " +
                "  ISNULL(o.create_buy_orders,  1)                             AS create_buy_orders, " +
                "  ISNULL(o.create_sell_orders, 1)                             AS create_sell_orders, " +
                "  ISNULL(wt.qty_purchased, 0)                                 AS bought_this_week " +
                "FROM entitydefaults ed " +
                "LEFT JOIN automarket_rawmat_overrides o ON o.definitionname = ed.definitionname " +
                "LEFT JOIN ( " +
                "    SELECT resource_name, unit_price FROM resource_market_prices " +
                "    WHERE calculated_on = (SELECT MAX(calculated_on) FROM resource_market_prices) " +
                ") rmp ON rmp.resource_name = ed.definitionname " +
                "LEFT JOIN ( " +
                "    SELECT definitionname, SUM(qty_purchased) AS qty_purchased " +
                "    FROM automarket_rawmat_weekly_tracking WHERE week_start >= @ws " +
                "    GROUP BY definitionname " +
                ") wt ON wt.definitionname = ed.definitionname " +
                "CROSS JOIN (SELECT param_value FROM automarket_config WHERE param_name = 'weekly_rawmat_cap_default') cfg " +
                "WHERE (ed.categoryflags & 276) = 276 AND ed.enabled = 1 AND ed.hidden = 0 " +
                "ORDER BY ed.definitionname";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var capOverride = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
                var buy  = reader.GetBoolean(4);
                var sell = reader.GetBoolean(5);
                result.Add(new AutoMarketCoveredMaterialRow
                {
                    DefinitionName      = reader.GetString(0),
                    CurrentPrice        = reader.IsDBNull(1) ? 0.0 : (double)reader.GetDecimal(1),
                    EffectiveCap        = reader.GetInt64(2),
                    WeeklyCapOverride   = capOverride,
                    CreateBuyOrders     = buy,
                    CreateSellOrders    = sell,
                    BoughtThisWeek      = reader.GetInt64(6),
                    OriginalCapOverride = capOverride,
                    OriginalBuyOrders   = buy,
                    OriginalSellOrders  = sell,
                });
            }
            return result;
        }
```

- [ ] **Step 4: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/AutoMarket/AutoMarketRepository.cs
git commit -m "IMPROVEMENT-040: repository — rename view ref, expand PricingTrace, add LoadCoveredMaterialsAsync"
```

---

## Task 8: Statistics VM + XAML — add Pricing Trace columns

**Files:**
- Modify: `src/Perpetuum.AdminTool/Views/AutoMarketStatisticsView.xaml`

The Statistics VM (`AutoMarketStatisticsViewModel`) needs no code changes — `LoadPricingTraceAsync` now returns rows with `BoughtThisWeek` and `EffectiveCap` populated. Only the XAML needs two new columns.

- [ ] **Step 1: Add columns to the Pricing Trace DataGrid**

In `AutoMarketStatisticsView.xaml`, find the Pricing Trace `<DataGrid.Columns>` block and add after the `Stored Price` column:

```xml
                        <DataGridTextColumn Header="Bought/Week"   Binding="{Binding BoughtThisWeek, StringFormat='{}{0:N0}'}" Width="110"/>
                        <DataGridTextColumn Header="Weekly Cap"    Binding="{Binding EffectiveCap,   StringFormat='{}{0:N0}'}" Width="110"/>
```

- [ ] **Step 2: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors, no BAML errors.

- [ ] **Step 3: Commit**

```
git add src/Perpetuum.AdminTool/Views/AutoMarketStatisticsView.xaml
git commit -m "IMPROVEMENT-040: Statistics Pricing Trace — add Bought/Week and Weekly Cap columns"
```

---

## Task 9: Raw Materials VM + View

**Files:**
- Create: `src/Perpetuum.AdminTool/ViewModels/AutoMarketRawMaterialsViewModel.cs`
- Create: `src/Perpetuum.AdminTool/Views/AutoMarketRawMaterialsView.xaml`
- Create: `src/Perpetuum.AdminTool/Views/AutoMarketRawMaterialsView.xaml.cs`

- [ ] **Step 1: Create `AutoMarketRawMaterialsViewModel.cs`**

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketRawMaterialsViewModel : ObservableObject
    {
        private readonly AutoMarketRepository   _repo;
        private readonly ChangeQueue            _queue;
        private readonly TranslationsViewModel? _translations;
        private const int EnglishLangId = 0;

        [ObservableProperty] private bool   _isLoading;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredRows))]
        private bool _showOverridesOnly;

        public ObservableCollection<AutoMarketCoveredMaterialRow> Rows { get; } = new();

        public System.Collections.Generic.IEnumerable<AutoMarketCoveredMaterialRow> FilteredRows =>
            _showOverridesOnly ? Rows.Where(r => r.HasOverride) : (System.Collections.Generic.IEnumerable<AutoMarketCoveredMaterialRow>)Rows;

        public AutoMarketRawMaterialsViewModel(
            AutoMarketRepository repo,
            ChangeQueue queue,
            TranslationsViewModel? translations = null)
        {
            _repo         = repo;
            _queue        = queue;
            _translations = translations;
        }

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        public async Task RefreshAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading raw materials...";
            StatusIsError = false;
            try
            {
                var rows  = await _repo.LoadCoveredMaterialsAsync();
                var store = _translations?.Store;

                Rows.Clear();
                foreach (var r in rows)
                {
                    if (store != null)
                    {
                        var tr = store.Rows.FirstOrDefault(x => x.Key == r.DefinitionName);
                        var t  = tr?[EnglishLangId];
                        if (!string.IsNullOrEmpty(t)) r.DisplayName = t;
                    }
                    if (string.IsNullOrEmpty(r.DisplayName)) r.DisplayName = r.DefinitionName;
                    Rows.Add(r);
                }

                OnPropertyChanged(nameof(FilteredRows));
                StatusMessage = $"Loaded {Rows.Count} materials at {DateTime.UtcNow:HH:mm:ss} UTC.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        private bool CanRefresh() => !IsLoading;
        partial void OnIsLoadingChanged(bool value) => RefreshCommand.NotifyCanExecuteChanged();

        [RelayCommand]
        private void QueueSave(AutoMarketCoveredMaterialRow row)
        {
            var description = $"automarket_rawmat_overrides: {row.DefinitionName}";
            var existing    = _queue.Items.FirstOrDefault(c => c.Description == description);
            if (existing != null) _queue.Items.Remove(existing);

            string sql;
            if (row.IsAtDefaults)
            {
                sql = $"DELETE FROM automarket_rawmat_overrides WHERE definitionname = {SqlLiteral.Of(row.DefinitionName)}";
            }
            else
            {
                sql =
                    $"MERGE automarket_rawmat_overrides AS t " +
                    $"USING (VALUES ({SqlLiteral.Of(row.DefinitionName)}, {SqlLiteral.OfNullableInt(row.WeeklyCapOverride)}, " +
                    $"{(row.CreateBuyOrders ? 1 : 0)}, {(row.CreateSellOrders ? 1 : 0)})) " +
                    $"AS s (definitionname, weekly_cap_override, create_buy_orders, create_sell_orders) " +
                    $"ON t.definitionname = s.definitionname " +
                    $"WHEN MATCHED THEN UPDATE SET " +
                    $"  weekly_cap_override = s.weekly_cap_override, " +
                    $"  create_buy_orders   = s.create_buy_orders, " +
                    $"  create_sell_orders  = s.create_sell_orders " +
                    $"WHEN NOT MATCHED THEN INSERT (definitionname, weekly_cap_override, create_buy_orders, create_sell_orders) " +
                    $"VALUES (s.definitionname, s.weekly_cap_override, s.create_buy_orders, s.create_sell_orders);";
            }

            _queue.Add(new RawSqlChange(description, sql));
            row.OriginalCapOverride  = row.WeeklyCapOverride; // not init; these are mutable for display
            StatusMessage = $"{row.DisplayName} queued.";
            OnPropertyChanged(nameof(FilteredRows));
        }
    }
}
```

- [ ] **Step 2: Create `AutoMarketRawMaterialsView.xaml.cs`**

```csharp
using System.Windows.Controls;

namespace Perpetuum.AdminTool.Views
{
    public partial class AutoMarketRawMaterialsView : UserControl
    {
        public AutoMarketRawMaterialsView()
        {
            InitializeComponent();
        }
    }
}
```

- [ ] **Step 3: Create `AutoMarketRawMaterialsView.xaml`**

```xml
<UserControl x:Class="Perpetuum.AdminTool.Views.AutoMarketRawMaterialsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="clr-namespace:Perpetuum.AdminTool.ViewModels"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             mc:Ignorable="d"
             d:DataContext="{d:DesignInstance Type=vm:AutoMarketRawMaterialsViewModel}">
    <DockPanel>

        <!-- Toolbar -->
        <Border DockPanel.Dock="Top" Background="#F2F2F2" Padding="8,6"
                BorderBrush="#DDD" BorderThickness="0,0,0,1">
            <DockPanel>
                <Button DockPanel.Dock="Right" Content="Refresh" Padding="10,2"
                        Command="{Binding RefreshCommand}"/>
                <CheckBox DockPanel.Dock="Right" Content="Overrides only"
                          VerticalAlignment="Center" Margin="0,0,12,0"
                          IsChecked="{Binding ShowOverridesOnly}"/>
                <TextBlock Text="{Binding StatusMessage}" VerticalAlignment="Center">
                    <TextBlock.Style>
                        <Style TargetType="TextBlock">
                            <Setter Property="Foreground" Value="DimGray"/>
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding StatusIsError}" Value="True">
                                    <Setter Property="Foreground" Value="DarkRed"/>
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </TextBlock.Style>
                </TextBlock>
            </DockPanel>
        </Border>

        <!-- Grid -->
        <DataGrid ItemsSource="{Binding FilteredRows}"
                  AutoGenerateColumns="False"
                  CanUserAddRows="False" CanUserDeleteRows="False"
                  HeadersVisibility="Column" GridLinesVisibility="Horizontal">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Name"          Binding="{Binding DisplayName}"     Width="200" IsReadOnly="True"/>
                <DataGridTextColumn Header="Price"         Binding="{Binding CurrentPrice,  StringFormat='{}{0:N2}'}" Width="100" IsReadOnly="True"/>
                <DataGridTextColumn Header="Eff. Cap"      Binding="{Binding EffectiveCap,  StringFormat='{}{0:N0}'}" Width="110" IsReadOnly="True"/>
                <DataGridTextColumn Header="Cap Override"  Binding="{Binding WeeklyCapOverride, StringFormat='{}{0:N0}'}" Width="110"/>
                <DataGridTextColumn Header="Bought/Week"   Binding="{Binding BoughtThisWeek,StringFormat='{}{0:N0}'}" Width="110" IsReadOnly="True"/>
                <DataGridCheckBoxColumn Header="Buy"       Binding="{Binding CreateBuyOrders}"  Width="50"/>
                <DataGridCheckBoxColumn Header="Sell"      Binding="{Binding CreateSellOrders}" Width="50"/>
                <DataGridTemplateColumn Width="80">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <Button Content="Queue Save" Padding="4,1" FontSize="10"
                                    Command="{Binding DataContext.QueueSaveCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                                    CommandParameter="{Binding}"/>
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>
            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</UserControl>
```

- [ ] **Step 4: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors, 0 BAML errors.

- [ ] **Step 5: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/AutoMarketRawMaterialsViewModel.cs
git add src/Perpetuum.AdminTool/Views/AutoMarketRawMaterialsView.xaml
git add src/Perpetuum.AdminTool/Views/AutoMarketRawMaterialsView.xaml.cs
git commit -m "IMPROVEMENT-040: Raw Materials VM and View"
```

---

## Task 10: Wire Raw Materials tab into `AutoMarketViewModel` and `AutoMarketView.xaml`

**Files:**
- Modify: `src/Perpetuum.AdminTool/ViewModels/AutoMarketViewModel.cs`
- Modify: `src/Perpetuum.AdminTool/Views/AutoMarketView.xaml`

- [ ] **Step 1: Add `RawMaterials` to `AutoMarketViewModel`**

Replace the file content:

```csharp
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Perpetuum.AdminTool.AutoMarket;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Editing;
using Perpetuum.AdminTool.Translations;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class AutoMarketViewModel : ObservableObject
    {
        private readonly AutoMarketRepository _repo;

        [ObservableProperty] private bool   _isRefreshing;
        [ObservableProperty] private string _statusMessage = "";
        [ObservableProperty] private bool   _statusIsError;

        public AutoMarketConfigViewModel        Config       { get; }
        public AutoMarketTradeListViewModel     TradeList    { get; }
        public AutoMarketRawMaterialsViewModel  RawMaterials { get; }
        public AutoMarketStatisticsViewModel    Statistics   { get; }
        public AutoMarketOrdersViewModel        Orders       { get; }

        public AutoMarketViewModel(
            AutoMarketRepository repo,
            ChangeQueue queue,
            LookupCache lookups,
            TranslationsViewModel? translations = null)
        {
            _repo        = repo;
            Config       = new AutoMarketConfigViewModel(repo, queue);
            TradeList    = new AutoMarketTradeListViewModel(repo, queue, lookups, translations);
            RawMaterials = new AutoMarketRawMaterialsViewModel(repo, queue, translations);
            Statistics   = new AutoMarketStatisticsViewModel(repo, translations);
            Orders       = new AutoMarketOrdersViewModel(repo, translations);
        }

        public async Task LoadAsync()
        {
            await Task.WhenAll(Config.LoadAsync(), TradeList.LoadAsync());
        }

        [RelayCommand(CanExecute = nameof(CanRefreshNow))]
        private async Task RefreshNow()
        {
            IsRefreshing  = true;
            StatusIsError = false;
            StatusMessage = "Refreshing AutoMarket orders...";
            try
            {
                await _repo.RefreshNowAsync();
                StatusMessage = $"Refresh complete at {DateTime.UtcNow:HH:mm:ss} UTC.";
            }
            catch (Exception ex)
            {
                StatusIsError = true;
                StatusMessage = $"Refresh failed: {ex.Message}";
            }
            finally { IsRefreshing = false; }
        }

        private bool CanRefreshNow() => !IsRefreshing;
        partial void OnIsRefreshingChanged(bool value) => RefreshNowCommand.NotifyCanExecuteChanged();
    }
}
```

- [ ] **Step 2: Add the Raw Materials tab to `AutoMarketView.xaml`**

In `AutoMarketView.xaml`, add the new tab between Trade List and Statistics. Add the `views` namespace import if not already there (it already is). Insert:

```xml
            <TabItem Header="Raw Materials">
                <views:AutoMarketRawMaterialsView DataContext="{Binding RawMaterials}"/>
            </TabItem>
```

Between the Trade List and Statistics tab items.

The updated `<TabControl>` block:

```xml
        <!-- Tabs -->
        <TabControl>
            <TabItem Header="Config">
                <views:AutoMarketConfigView DataContext="{Binding Config}"/>
            </TabItem>
            <TabItem Header="Trade List">
                <views:AutoMarketTradeListView DataContext="{Binding TradeList}"/>
            </TabItem>
            <TabItem Header="Raw Materials">
                <views:AutoMarketRawMaterialsView DataContext="{Binding RawMaterials}"/>
            </TabItem>
            <TabItem Header="Statistics">
                <views:AutoMarketStatisticsView DataContext="{Binding Statistics}"/>
            </TabItem>
            <TabItem Header="Orders">
                <views:AutoMarketOrdersView DataContext="{Binding Orders}"/>
            </TabItem>
        </TabControl>
```

- [ ] **Step 3: Build**

```
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```
git add src/Perpetuum.AdminTool/ViewModels/AutoMarketViewModel.cs
git add src/Perpetuum.AdminTool/Views/AutoMarketView.xaml
git commit -m "IMPROVEMENT-040: wire Raw Materials tab into AutoMarketViewModel and AutoMarketView"
```

---

## Task 11: Manual validation

- [ ] **Step 1: Apply migration to the database**

Run `docs/db_structure/migrations/IMPROVEMENT-040-rawmat-decoupling.sql` in SSMS against perpetuumsa. Verify:
```sql
SELECT * FROM automarket_config WHERE param_name = 'weekly_rawmat_cap_default';
-- Expected: row with param_value = 500000000

SELECT OBJECT_ID('dbo.automarket_rawmat_overrides');
SELECT OBJECT_ID('dbo.automarket_rawmat_weekly_tracking');
SELECT OBJECT_ID('dbo.v_trade_list_raw_material_demand');
-- Expected: non-null for all three

SELECT OBJECT_ID('dbo.v_required_raw_materials');
-- Expected: NULL (renamed)
```

- [ ] **Step 2: Verify `recalculate_raw_material_prices` covers more materials**

```sql
-- Count before running (from existing data):
SELECT COUNT(DISTINCT resource_name) FROM resource_market_prices;

EXEC recalculate_raw_material_prices;

-- Count after:
SELECT COUNT(DISTINCT resource_name) FROM resource_market_prices
WHERE calculated_on = (SELECT MAX(calculated_on) FROM resource_market_prices);
-- Expected: equal to or greater than the entitydefaults count:
SELECT COUNT(*) FROM entitydefaults WHERE (categoryflags & 276) = 276 AND enabled = 1 AND hidden = 0;
```

- [ ] **Step 3: Verify `usp_RefreshAutoMarketOrders` places orders for expanded material set**

```sql
EXEC usp_RefreshAutoMarketOrders;

-- All cf_raw_material buy orders placed:
SELECT COUNT(*) FROM marketitems mi
JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
WHERE mi.isAutoOrder = 1 AND mi.isSell = 0
  AND (ed.categoryflags & 276) = 276;

-- Materials with create_buy_orders = 0 have no buy orders:
INSERT INTO automarket_rawmat_overrides (definitionname, create_buy_orders, create_sell_orders)
SELECT TOP 1 definitionname, 0, 1
FROM entitydefaults WHERE (categoryflags & 276) = 276 AND enabled = 1 AND hidden = 0;
-- Note the definitionname you just inserted.

EXEC usp_RefreshAutoMarketOrders;

SELECT COUNT(*) FROM marketitems mi
JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
WHERE mi.isAutoOrder = 1 AND mi.isSell = 0
  AND ed.definitionname = '<the definitionname you inserted>';
-- Expected: 0

-- Clean up test override:
DELETE FROM automarket_rawmat_overrides WHERE create_buy_orders = 0;
```

- [ ] **Step 4: Verify weekly cap enforcement**

```sql
-- Insert an override with a very low cap:
INSERT INTO automarket_rawmat_overrides (definitionname, weekly_cap_override)
SELECT TOP 1 definitionname, 5 FROM entitydefaults WHERE (categoryflags & 276) = 276 AND enabled = 1 AND hidden = 0;

EXEC usp_RefreshAutoMarketOrders;

-- Verify buy order quantity <= 5 for the capped material:
SELECT mi.quantity FROM marketitems mi
JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
JOIN automarket_rawmat_overrides o ON o.definitionname = ed.definitionname
WHERE mi.isAutoOrder = 1 AND mi.isSell = 0 AND o.weekly_cap_override = 5;
-- Expected: quantity <= 5

-- Clean up:
DELETE FROM automarket_rawmat_overrides WHERE weekly_cap_override = 5;
```

- [ ] **Step 5: Launch AdminTool, open AutoMarket panel**

- Config tab: verify `weekly_rawmat_cap_default` row appears with label "Weekly Rawmat Cap (default, 0=∞)".
- Raw Materials tab: click Refresh — verify rows appear for all qualifying raw materials with correct columns.
- Set a cap override and "Queue Save" on one row — verify the ChangeQueue entry appears.
- Toggle "Overrides only" — verify list filters to only rows with overrides.
- Statistics tab → Pricing Trace: verify "Bought/Week" and "Weekly Cap" columns appear.

- [ ] **Step 6: Final commit / update backlog**

Update `docs/backlog/improvements.md` — change IMPROVEMENT-040 status from `TODO` to `DONE` and add an implementation summary section.

```
git add docs/backlog/improvements.md
git commit -m "IMPROVEMENT-040: mark DONE in backlog"
```
