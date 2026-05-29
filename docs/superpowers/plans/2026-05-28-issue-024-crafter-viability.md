# ISSUE-024 AutoMarket Crafter Viability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a production sell price margin, reduce raw material sell markup, add product buyback buy orders, and cap raw material purchase spending — repositioning AutoMarket as a price backstop rather than a market maker so player crafters have a viable economic role.

**Architecture:** Schema-first (new table + config rows → new stored proc → two altered stored procs → C# hook). Each layer is validated before the next depends on it. All SQL changes ship as both live DDL (run in SSMS) and updated doc `.sql` files. All SQL is idempotent (`CREATE OR ALTER PROCEDURE`, `IF OBJECT_ID IS NULL` guards, `MERGE` for seed rows).

**Tech Stack:** SQL Server T-SQL, C# 12 / .NET 8, `Db.Query()` pattern, `CategoryFlags.cf_raw_material`, `TransactionScope`.

**Spec:** `docs/superpowers/specs/2026-05-28-issue-024-automarket-crafter-viability-design.md`

---

## File Map

| File | Action |
|---|---|
| SQL Server (live DB) | New table `rawmat_purchased`; 4 new rows in `automarket_config`; new proc `sp_RecordRawMatPurchased`; ALTER `recalculate_raw_material_prices`; ALTER `usp_RefreshAutoMarketOrders` |
| `docs/db_structure/migrations/20260528_issue_024_crafter_viability.sql` | New — idempotent DDL for table + config rows |
| `docs/db_structure/stored_procedures/dbo.sp_RecordRawMatPurchased.StoredProcedure.sql` | New — `CREATE OR ALTER PROCEDURE` |
| `docs/db_structure/stored_procedures/dbo.recalculate_raw_material_prices.StoredProcedure.sql` | Add rawmat cleanup line to 90-day window block |
| `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql` | Replace with updated version (Step 0 filter, Steps 3/4/5 multipliers, new Step 6) |
| `docs/db_structure/database_schema_documentation.md` | Add `rawmat_purchased` table entry; add 4 rows to `automarket_config` seeded rows |
| `src/Perpetuum/Services/MarketEngine/Market.cs` | Add raw material purchase recording at 3 locations in `FulfillSellOrderInstantly` |

---

## Task 1: Schema changes — `rawmat_purchased` table and `automarket_config` seed rows

**Files:**
- Create: `docs/db_structure/migrations/20260528_issue_024_crafter_viability.sql`
- Modify: `docs/db_structure/database_schema_documentation.md`

- [ ] **Step 1.1: Execute DDL in SSMS**

Run this in SSMS against the `perpetuumsa` database:

```sql
BEGIN TRANSACTION;

-- New table: daily raw material purchase tracking (mirrors plasma_sold)
IF OBJECT_ID('dbo.rawmat_purchased', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.rawmat_purchased (
        purchased_on    DATE   NOT NULL,
        item_definition INT    NOT NULL,
        quantity        BIGINT NOT NULL,
        income          FLOAT  NOT NULL,
        CONSTRAINT PK_rawmat_purchased PRIMARY KEY (purchased_on, item_definition)
    );
END;

-- New config params for ISSUE-024
MERGE INTO dbo.automarket_config AS target
USING (VALUES
    ('product_sell_margin',     1.2),
    ('raw_mat_sell_multiplier', 1.5),
    ('product_buyback_margin',  0.80),
    ('daily_rawmat_budget_nic', 5000000.0)
) AS src (param_name, param_value)
ON target.param_name = src.param_name
WHEN NOT MATCHED THEN
    INSERT (param_name, param_value) VALUES (src.param_name, src.param_value);

COMMIT;
```

- [ ] **Step 1.2: Verify table and config rows**

```sql
-- Table exists
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'rawmat_purchased'
ORDER BY ORDINAL_POSITION;
-- Expected: 4 rows — purchased_on DATE NOT NULL, item_definition INT NOT NULL,
--           quantity BIGINT NOT NULL, income FLOAT NOT NULL

-- Config rows exist
SELECT param_name, param_value FROM automarket_config
WHERE param_name IN ('product_sell_margin','raw_mat_sell_multiplier',
                     'product_buyback_margin','daily_rawmat_budget_nic')
ORDER BY param_name;
-- Expected: 4 rows with values 0.8, 5000000, 1.2, 1.5
```

- [ ] **Step 1.3: Create migration file**

Create `docs/db_structure/migrations/20260528_issue_024_crafter_viability.sql` with the exact DDL from Step 1.1.

```sql
-- ISSUE-024: AutoMarket Crafter Viability
-- Creates rawmat_purchased tracking table and seeds new automarket_config params.
-- Run sp changes separately via the updated .sql files in docs/db_structure/stored_procedures/
-- in this order: sp_RecordRawMatPurchased, recalculate_raw_material_prices, usp_RefreshAutoMarketOrders.

BEGIN TRANSACTION;

-- New table: daily raw material purchase tracking (mirrors plasma_sold)
IF OBJECT_ID('dbo.rawmat_purchased', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.rawmat_purchased (
        purchased_on    DATE   NOT NULL,
        item_definition INT    NOT NULL,
        quantity        BIGINT NOT NULL,
        income          FLOAT  NOT NULL,
        CONSTRAINT PK_rawmat_purchased PRIMARY KEY (purchased_on, item_definition)
    );
END;

-- New config params for ISSUE-024
MERGE INTO dbo.automarket_config AS target
USING (VALUES
    ('product_sell_margin',     1.2),
    ('raw_mat_sell_multiplier', 1.5),
    ('product_buyback_margin',  0.80),
    ('daily_rawmat_budget_nic', 5000000.0)
) AS src (param_name, param_value)
ON target.param_name = src.param_name
WHEN NOT MATCHED THEN
    INSERT (param_name, param_value) VALUES (src.param_name, src.param_value);

COMMIT;
```

- [ ] **Step 1.4: Update schema documentation**

In `docs/db_structure/database_schema_documentation.md`:

**a)** Find the `automarket_config` seeded rows table and add 4 new rows:

```markdown
| `product_sell_margin`     | `1.2`       |
| `raw_mat_sell_multiplier` | `1.5`       |
| `product_buyback_margin`  | `0.80`      |
| `daily_rawmat_budget_nic` | `5000000.0` |
```

**b)** Find the alphabetically correct position for `rawmat_purchased` (after `raw_material_prices`, before `resource_market_prices` or similar) and add:

```markdown
## rawmat_purchased

**Schema:** `dbo`

### Purpose

Daily tracking of NIC paid out for raw material AutoMarket buy order fulfillments. Used by `usp_RefreshAutoMarketOrders` to enforce the `daily_rawmat_budget_nic` cap. Populated by `sp_RecordRawMatPurchased` (called from `Market.cs`). Rolling 90-day window maintained by `recalculate_raw_material_prices`.

### Columns

| Column | Definition |
|---|---|
| `purchased_on` | `date [not null, pk]` |
| `item_definition` | `int [not null, pk]` |
| `quantity` | `bigint [not null]` |
| `income` | `float [not null]` |

---
```

- [ ] **Step 1.5: Commit**

```bash
git add docs/db_structure/migrations/20260528_issue_024_crafter_viability.sql
git add docs/db_structure/database_schema_documentation.md
git commit -m "feat(db): add rawmat_purchased table and ISSUE-024 automarket_config params"
```

---

## Task 2: Create `sp_RecordRawMatPurchased` + extend `recalculate_raw_material_prices`

**Files:**
- Create: `docs/db_structure/stored_procedures/dbo.sp_RecordRawMatPurchased.StoredProcedure.sql`
- Modify: `docs/db_structure/stored_procedures/dbo.recalculate_raw_material_prices.StoredProcedure.sql`

- [ ] **Step 2.1: Create `sp_RecordRawMatPurchased` in SSMS**

```sql
CREATE OR ALTER PROCEDURE [dbo].[sp_RecordRawMatPurchased]
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

- [ ] **Step 2.2: Verify `sp_RecordRawMatPurchased` works**

```sql
-- Insert two rows for the same item on the same day (should merge to one row)
EXEC sp_RecordRawMatPurchased
    @purchased_on = '2026-01-01', @item_def = 999999, @quantity = 100, @income = 1000.0;
EXEC sp_RecordRawMatPurchased
    @purchased_on = '2026-01-01', @item_def = 999999, @quantity = 50,  @income = 500.0;

SELECT * FROM rawmat_purchased WHERE item_definition = 999999;
-- Expected: 1 row — purchased_on='2026-01-01', item_definition=999999,
--           quantity=150, income=1500.0

-- Clean up
DELETE FROM rawmat_purchased WHERE item_definition = 999999;
```

- [ ] **Step 2.3: Create the doc file**

Create `docs/db_structure/stored_procedures/dbo.sp_RecordRawMatPurchased.StoredProcedure.sql`:

```sql
USE [perpetuumsa]
GO
/****** Object:  StoredProcedure [dbo].[sp_RecordRawMatPurchased]    Script Date: 28.05.2026 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

---- Upsert raw material AutoMarket purchase record for daily NIC budget tracking

CREATE OR ALTER PROCEDURE [dbo].[sp_RecordRawMatPurchased]
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

- [ ] **Step 2.4: Alter `recalculate_raw_material_prices` in SSMS to add rawmat cleanup**

The only change is adding one DELETE line to the existing 90-day cleanup block. Execute:

```sql
CREATE OR ALTER PROCEDURE [dbo].[recalculate_raw_material_prices]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @today      DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @week_start DATE = DATEADD(DAY, -DATEPART(WEEKDAY, @today) + 2, @today);
    DECLARE @start_date DATE = DATEADD(DAY, -7, @today);

    DECLARE @anchor_fraction FLOAT = (
        SELECT param_value FROM automarket_config WHERE param_name = 'plasma_anchor_fraction'
    );
    DECLARE @ds_min FLOAT = (
        SELECT param_value FROM automarket_config WHERE param_name = 'resource_ds_ratio_min'
    );
    DECLARE @ds_max FLOAT = (
        SELECT param_value FROM automarket_config WHERE param_name = 'resource_ds_ratio_max'
    );

    -- Alpha common plasma price as the anchor
    DECLARE @plasma_anchor FLOAT = (
        SELECT TOP 1 dynamic_price
        FROM fn_CalculateDynamicPlasmaPrices(1)
        WHERE plasma_type = 'def_common_reactor_plasma'
    ) * @anchor_fraction;

    -- Compute and upsert new prices for all raw materials in the production chain
    WITH
    supply AS (
        SELECT
            resource_name,
            SUM(CASE WHEN is_pvp = 1 THEN quantity ELSE 0 END) AS pvp_qty,
            SUM(quantity)                                        AS total_qty,
            SUM(quantity) / 7.0                                  AS supply_daily_avg
        FROM resources_gathered
        WHERE gathered_on >= @start_date
        GROUP BY resource_name
    ),
    demand_cte AS (
        SELECT raw_material, SUM(total_quantity) / 7.0 AS daily_demand
        FROM v_required_raw_materials
        GROUP BY raw_material
    ),
    materials AS (
        SELECT DISTINCT raw_material AS resource_name
        FROM v_required_raw_materials
    ),
    priced AS (
        SELECT
            m.resource_name,
            ROUND(
                @plasma_anchor
                * CASE
                    WHEN s.supply_daily_avg IS NULL OR s.supply_daily_avg = 0
                        THEN @ds_max
                    ELSE
                        CASE
                            WHEN ISNULL(d.daily_demand, 0) / s.supply_daily_avg < @ds_min THEN @ds_min
                            WHEN ISNULL(d.daily_demand, 0) / s.supply_daily_avg > @ds_max THEN @ds_max
                            ELSE ISNULL(d.daily_demand, 0) / s.supply_daily_avg
                        END
                  END
                * (1.0 + ISNULL(
                    CAST(s.pvp_qty AS FLOAT) / NULLIF(s.total_qty, 0),
                    1.0
                  )),
                2
            ) AS new_price
        FROM materials m
        LEFT JOIN supply     s ON s.resource_name = m.resource_name
        LEFT JOIN demand_cte d ON d.raw_material  = m.resource_name
    )
    MERGE INTO dbo.resource_market_prices AS target
    USING priced AS source
    ON  target.calculated_on  = @week_start
    AND target.resource_name COLLATE DATABASE_DEFAULT = source.resource_name COLLATE DATABASE_DEFAULT
    WHEN MATCHED THEN
        UPDATE SET unit_price = source.new_price
    WHEN NOT MATCHED THEN
        INSERT (calculated_on, resource_name, unit_price)
        VALUES (@week_start, source.resource_name, source.new_price);

    -- Cleanup old stats (90-day rolling window)
    DELETE FROM plasma_gathered    WHERE gathered_on  < DATEADD(DAY, -90, @today);
    DELETE FROM plasma_sold        WHERE sold_on       < DATEADD(DAY, -90, @today);
    DELETE FROM resources_gathered WHERE gathered_on  < DATEADD(DAY, -90, @today);
    DELETE FROM rawmat_purchased   WHERE purchased_on  < DATEADD(DAY, -90, @today);
END;
GO
```

- [ ] **Step 2.5: Verify cleanup line is present**

```sql
SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.recalculate_raw_material_prices'));
-- Expected: 'rawmat_purchased' appears in the output
```

- [ ] **Step 2.6: Update `recalculate_raw_material_prices` doc file**

Replace the full content of `docs/db_structure/stored_procedures/dbo.recalculate_raw_material_prices.StoredProcedure.sql` with:

```sql
USE [perpetuumsa]
GO
/****** Object:  StoredProcedure [dbo].[recalculate_raw_material_prices]    Script Date: 28.05.2026 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

---- Dynamic supply/demand + PvP-risk formula anchored to live plasma prices

CREATE OR ALTER PROCEDURE [dbo].[recalculate_raw_material_prices]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @today      DATE = CAST(GETUTCDATE() AS DATE);
    DECLARE @week_start DATE = DATEADD(DAY, -DATEPART(WEEKDAY, @today) + 2, @today);
    DECLARE @start_date DATE = DATEADD(DAY, -7, @today);

    DECLARE @anchor_fraction FLOAT = (
        SELECT param_value FROM automarket_config WHERE param_name = 'plasma_anchor_fraction'
    );
    DECLARE @ds_min FLOAT = (
        SELECT param_value FROM automarket_config WHERE param_name = 'resource_ds_ratio_min'
    );
    DECLARE @ds_max FLOAT = (
        SELECT param_value FROM automarket_config WHERE param_name = 'resource_ds_ratio_max'
    );

    -- Alpha common plasma price as the anchor
    DECLARE @plasma_anchor FLOAT = (
        SELECT TOP 1 dynamic_price
        FROM fn_CalculateDynamicPlasmaPrices(1)
        WHERE plasma_type = 'def_common_reactor_plasma'
    ) * @anchor_fraction;

    -- Compute and upsert new prices for all raw materials in the production chain
    WITH
    supply AS (
        SELECT
            resource_name,
            SUM(CASE WHEN is_pvp = 1 THEN quantity ELSE 0 END) AS pvp_qty,
            SUM(quantity)                                        AS total_qty,
            SUM(quantity) / 7.0                                  AS supply_daily_avg
        FROM resources_gathered
        WHERE gathered_on >= @start_date
        GROUP BY resource_name
    ),
    demand_cte AS (
        SELECT raw_material, SUM(total_quantity) / 7.0 AS daily_demand
        FROM v_required_raw_materials
        GROUP BY raw_material
    ),
    materials AS (
        SELECT DISTINCT raw_material AS resource_name
        FROM v_required_raw_materials
    ),
    priced AS (
        SELECT
            m.resource_name,
            ROUND(
                @plasma_anchor
                * CASE
                    WHEN s.supply_daily_avg IS NULL OR s.supply_daily_avg = 0
                        THEN @ds_max
                    ELSE
                        CASE
                            WHEN ISNULL(d.daily_demand, 0) / s.supply_daily_avg < @ds_min THEN @ds_min
                            WHEN ISNULL(d.daily_demand, 0) / s.supply_daily_avg > @ds_max THEN @ds_max
                            ELSE ISNULL(d.daily_demand, 0) / s.supply_daily_avg
                        END
                  END
                * (1.0 + ISNULL(
                    CAST(s.pvp_qty AS FLOAT) / NULLIF(s.total_qty, 0),
                    1.0
                  )),
                2
            ) AS new_price
        FROM materials m
        LEFT JOIN supply     s ON s.resource_name = m.resource_name
        LEFT JOIN demand_cte d ON d.raw_material  = m.resource_name
    )
    MERGE INTO dbo.resource_market_prices AS target
    USING priced AS source
    ON  target.calculated_on  = @week_start
    AND target.resource_name COLLATE DATABASE_DEFAULT = source.resource_name COLLATE DATABASE_DEFAULT
    WHEN MATCHED THEN
        UPDATE SET unit_price = source.new_price
    WHEN NOT MATCHED THEN
        INSERT (calculated_on, resource_name, unit_price)
        VALUES (@week_start, source.resource_name, source.new_price);

    -- Cleanup old stats (90-day rolling window)
    DELETE FROM plasma_gathered    WHERE gathered_on  < DATEADD(DAY, -90, @today);
    DELETE FROM plasma_sold        WHERE sold_on       < DATEADD(DAY, -90, @today);
    DELETE FROM resources_gathered WHERE gathered_on  < DATEADD(DAY, -90, @today);
    DELETE FROM rawmat_purchased   WHERE purchased_on  < DATEADD(DAY, -90, @today);
END;
GO
```

- [ ] **Step 2.7: Commit**

```bash
git add docs/db_structure/stored_procedures/dbo.sp_RecordRawMatPurchased.StoredProcedure.sql
git add docs/db_structure/stored_procedures/dbo.recalculate_raw_material_prices.StoredProcedure.sql
git commit -m "feat(db): add sp_RecordRawMatPurchased; extend recalculate_raw_material_prices with rawmat cleanup (ISSUE-024)"
```

---

## Task 3: Modify `usp_RefreshAutoMarketOrders`

**Files:**
- Modify: `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql`

Five changes from spec: Step 0 production item filter, Step 3 sell margin, Step 4 rawmat budget cap, Step 5 sell multiplier, new Step 6 buyback orders.

- [ ] **Step 3.1: Execute `CREATE OR ALTER PROCEDURE` in SSMS**

Run the following against `perpetuumsa`:

```sql
CREATE OR ALTER PROCEDURE [dbo].[usp_RefreshAutoMarketOrders]
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        DECLARE @marketeid  BIGINT;
        DECLARE @vendoreid  BIGINT;

        -- Step 0: Snapshot unsold and unbought items
        DELETE FROM [automarket_unsold_leftovers];
        DELETE FROM [automarket_unbought_resources];

        INSERT INTO [automarket_unsold_leftovers] (itemdefinition, quantity)
        SELECT itemdefinition, SUM(CAST(quantity AS BIGINT))
        FROM marketitems
        WHERE isAutoOrder = 1 AND isSell = 1
        GROUP BY itemdefinition;

        -- Unbought mats: exclude plasma (3271-3274) AND production items from market_orders_configuration
        INSERT INTO automarket_unbought_resources (itemdefinition, quantity)
        SELECT mi.itemdefinition, SUM(CAST(mi.quantity AS BIGINT))
        FROM marketitems mi
        INNER JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
        WHERE mi.isAutoOrder = 1 AND mi.isSell = 0
          AND mi.itemdefinition NOT IN (3271, 3272, 3273, 3274)
          AND NOT EXISTS (
              SELECT 1 FROM market_orders_configuration moc
              WHERE moc.definitionname = ed.definitionname
          )
        GROUP BY mi.itemdefinition;

        -- Step 1: Remove old auto orders
        DELETE FROM marketitems WHERE isAutoOrder = 1;

        -- Materialise expensive recursive-CTE views once so Steps 3-6 do not re-evaluate them.
        SELECT product, production_cost_nic
        INTO #prod_costs
        FROM v_all_production_costs;

        CREATE INDEX IX_pc_product ON #prod_costs (product);

        SELECT product, raw_material, total_quantity
        INTO #raw_materials
        FROM v_required_raw_materials;

        CREATE INDEX IX_rm_product ON #raw_materials (product);
        CREATE INDEX IX_rm_raw     ON #raw_materials (raw_material);

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
        INNER JOIN #prod_costs pc    ON moc.definitionname = pc.product;

        -- Step 4: Raw material buy orders — skip if daily budget exhausted
        ;WITH NeedProducts AS (
            SELECT
                moc.definitionname AS product,
                CAST(moc.amount - ISNULL(us.quantity, 0) AS BIGINT) AS need_amount
            FROM market_orders_configuration moc
            INNER JOIN entitydefaults ed ON moc.definitionname = ed.definitionname
            LEFT JOIN automarket_unsold_leftovers us ON ed.definition = us.itemdefinition
        ),
        RequiredRaw AS (
            SELECT
                ed.definition AS raw_material_def,
                SUM(rm.total_quantity * np.need_amount) AS required_from_products
            FROM NeedProducts np
            INNER JOIN #raw_materials rm ON rm.product = np.product
            INNER JOIN entitydefaults ed ON ed.definitionname = rm.raw_material
            WHERE np.need_amount > 0
            GROUP BY ed.definition
        ),
        Unbought AS (
            SELECT
                ub.itemdefinition AS raw_material_def,
                SUM(ub.quantity)  AS required_from_unbought
            FROM automarket_unbought_resources ub
            GROUP BY ub.itemdefinition
        ),
        Combined AS (
            SELECT
                COALESCE(r.raw_material_def, u.raw_material_def) AS combined_def,
                COALESCE(r.required_from_products, 0) + COALESCE(u.required_from_unbought, 0) AS total_required_quantity
            FROM RequiredRaw r
            FULL OUTER JOIN Unbought u ON u.raw_material_def = r.raw_material_def
        )
        INSERT INTO marketitems (
            marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
        )
        SELECT
            @marketeid,
            c.combined_def,
            @vendoreid,
            0,
            0,
            apc.production_cost_nic,
            c.total_required_quantity,
            1,
            1
        FROM Combined c
        INNER JOIN entitydefaults ed ON ed.definition = c.combined_def
        INNER JOIN #prod_costs apc  ON ed.definitionname = apc.product
        WHERE c.total_required_quantity > 0
          AND @remaining_rawmat_budget > 0;

        -- Step 5: Raw resource sell orders — price at cost * raw_mat_sell_multiplier
        INSERT INTO marketitems (
            marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
        )
        SELECT
            @marketeid,
            ed.definition,
            @vendoreid,
            0,
            1,
            apc.production_cost_nic * @raw_mat_sell_multiplier,
            10000000,
            1,
            1
        FROM #raw_materials rrm
        INNER JOIN entitydefaults ed ON rrm.raw_material = ed.definitionname
        INNER JOIN #prod_costs apc  ON rrm.raw_material  = apc.product
        GROUP BY ed.definition, apc.production_cost_nic;

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
        INNER JOIN #prod_costs pc    ON moc.definitionname = pc.product;

    END TRY
    BEGIN CATCH
        PRINT 'Error in usp_RefreshAutoMarketOrders: ' + ERROR_MESSAGE();
        THROW;
    END CATCH
END;
GO
```

- [ ] **Step 3.2: Verify product sell prices are at 1.2×**

```sql
EXEC usp_RefreshAutoMarketOrders;

-- Product sell orders should be at production_cost * 1.2
-- Join market_orders_configuration to identify production items (excludes plasma and raw mats)
SELECT mi.price, pc.production_cost_nic, mi.price / pc.production_cost_nic AS ratio,
       ed.definitionname
FROM marketitems mi
JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
JOIN v_all_production_costs pc ON ed.definitionname = pc.product
JOIN market_orders_configuration moc ON moc.definitionname = ed.definitionname
WHERE mi.isAutoOrder = 1 AND mi.isSell = 1
ORDER BY ed.definitionname;
-- Expected: ratio column ≈ 1.2 for all production item sell orders
```

- [ ] **Step 3.3: Verify raw material sell prices are at 1.5×**

```sql
SELECT mi.price, pc.production_cost_nic, mi.price / pc.production_cost_nic AS ratio,
       ed.definitionname
FROM marketitems mi
JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
JOIN v_all_production_costs pc ON ed.definitionname = pc.product
WHERE mi.isAutoOrder = 1 AND mi.isSell = 1
  AND mi.quantity = 10000000
ORDER BY ed.definitionname;
-- Expected: ratio column ≈ 1.5 for all raw material sell orders
```

- [ ] **Step 3.4: Verify product buyback orders exist**

```sql
SELECT mi.price, pc.production_cost_nic, mi.price / pc.production_cost_nic AS ratio,
       ed.definitionname
FROM marketitems mi
JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
JOIN v_all_production_costs pc ON ed.definitionname = pc.product
JOIN market_orders_configuration moc ON moc.definitionname = ed.definitionname
WHERE mi.isAutoOrder = 1 AND mi.isSell = 0
  AND ed.definitionname NOT LIKE '%plasma%'
ORDER BY ed.definitionname;
-- Expected: ratio column ≈ 0.80 for all production item buyback orders
-- Row count should match the number of rows in market_orders_configuration
```

- [ ] **Step 3.5: Verify buyback orders are excluded from `automarket_unbought_resources` on next refresh**

```sql
-- Run refresh again to simulate next cycle
EXEC usp_RefreshAutoMarketOrders;

-- Check that automarket_unbought_resources contains only raw materials, not production items
SELECT ubr.itemdefinition, ed.definitionname
FROM automarket_unbought_resources ubr
JOIN entitydefaults ed ON ed.definition = ubr.itemdefinition
-- Should NOT contain any definitionname that is in market_orders_configuration
WHERE EXISTS (
    SELECT 1 FROM market_orders_configuration moc WHERE moc.definitionname = ed.definitionname
);
-- Expected: 0 rows
```

- [ ] **Step 3.6: Verify rawmat budget cap**

```sql
-- Set budget to 1 NIC to force all raw mat buy orders to be skipped
UPDATE automarket_config SET param_value = 1 WHERE param_name = 'daily_rawmat_budget_nic';

EXEC usp_RefreshAutoMarketOrders;

SELECT COUNT(*) AS raw_mat_buy_orders
FROM marketitems mi
JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
WHERE mi.isAutoOrder = 1 AND mi.isSell = 0
  AND ed.definitionname NOT LIKE '%plasma%'
  AND NOT EXISTS (
      SELECT 1 FROM market_orders_configuration moc WHERE moc.definitionname = ed.definitionname
  );
-- Expected: 0

-- Restore budget
UPDATE automarket_config SET param_value = 5000000 WHERE param_name = 'daily_rawmat_budget_nic';
EXEC usp_RefreshAutoMarketOrders;
```

- [ ] **Step 3.7: Update the doc file**

Replace the full content of `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql` with:

```sql
USE [perpetuumsa]
GO
/****** Object:  StoredProcedure [dbo].[usp_RefreshAutoMarketOrders]    Script Date: 28.05.2026 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

---- Place auto market orders: plasma buy orders with daily budget cap; raw material orders with
---- daily NIC budget cap; product sell orders at margin; raw material sell orders at multiplier;
---- product buyback buy orders at backstop price.
---- Cursors replaced with set-based INSERTs. Views materialised into temp tables to avoid
---- recursive-CTE re-evaluation.

CREATE OR ALTER PROCEDURE [dbo].[usp_RefreshAutoMarketOrders]
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        DECLARE @marketeid  BIGINT;
        DECLARE @vendoreid  BIGINT;

        -- Step 0: Snapshot unsold and unbought items
        DELETE FROM [automarket_unsold_leftovers];
        DELETE FROM [automarket_unbought_resources];

        INSERT INTO [automarket_unsold_leftovers] (itemdefinition, quantity)
        SELECT itemdefinition, SUM(CAST(quantity AS BIGINT))
        FROM marketitems
        WHERE isAutoOrder = 1 AND isSell = 1
        GROUP BY itemdefinition;

        -- Unbought mats: exclude plasma (3271-3274) AND production items (market_orders_configuration)
        INSERT INTO automarket_unbought_resources (itemdefinition, quantity)
        SELECT mi.itemdefinition, SUM(CAST(mi.quantity AS BIGINT))
        FROM marketitems mi
        INNER JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
        WHERE mi.isAutoOrder = 1 AND mi.isSell = 0
          AND mi.itemdefinition NOT IN (3271, 3272, 3273, 3274)
          AND NOT EXISTS (
              SELECT 1 FROM market_orders_configuration moc
              WHERE moc.definitionname = ed.definitionname
          )
        GROUP BY mi.itemdefinition;

        -- Step 1: Remove old auto orders
        DELETE FROM marketitems WHERE isAutoOrder = 1;

        -- Materialise expensive recursive-CTE views once so Steps 3-6 do not re-evaluate them.
        SELECT product, production_cost_nic
        INTO #prod_costs
        FROM v_all_production_costs;

        CREATE INDEX IX_pc_product ON #prod_costs (product);

        SELECT product, raw_material, total_quantity
        INTO #raw_materials
        FROM v_required_raw_materials;

        CREATE INDEX IX_rm_product ON #raw_materials (product);
        CREATE INDEX IX_rm_raw     ON #raw_materials (raw_material);

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
        INNER JOIN #prod_costs pc    ON moc.definitionname = pc.product;

        -- Step 4: Raw material buy orders — skip all if daily budget exhausted
        ;WITH NeedProducts AS (
            SELECT
                moc.definitionname AS product,
                CAST(moc.amount - ISNULL(us.quantity, 0) AS BIGINT) AS need_amount
            FROM market_orders_configuration moc
            INNER JOIN entitydefaults ed ON moc.definitionname = ed.definitionname
            LEFT JOIN automarket_unsold_leftovers us ON ed.definition = us.itemdefinition
        ),
        RequiredRaw AS (
            SELECT
                ed.definition AS raw_material_def,
                SUM(rm.total_quantity * np.need_amount) AS required_from_products
            FROM NeedProducts np
            INNER JOIN #raw_materials rm ON rm.product = np.product
            INNER JOIN entitydefaults ed ON ed.definitionname = rm.raw_material
            WHERE np.need_amount > 0
            GROUP BY ed.definition
        ),
        Unbought AS (
            SELECT
                ub.itemdefinition AS raw_material_def,
                SUM(ub.quantity)  AS required_from_unbought
            FROM automarket_unbought_resources ub
            GROUP BY ub.itemdefinition
        ),
        Combined AS (
            SELECT
                COALESCE(r.raw_material_def, u.raw_material_def) AS combined_def,
                COALESCE(r.required_from_products, 0) + COALESCE(u.required_from_unbought, 0) AS total_required_quantity
            FROM RequiredRaw r
            FULL OUTER JOIN Unbought u ON u.raw_material_def = r.raw_material_def
        )
        INSERT INTO marketitems (
            marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
        )
        SELECT
            @marketeid,
            c.combined_def,
            @vendoreid,
            0,
            0,
            apc.production_cost_nic,
            c.total_required_quantity,
            1,
            1
        FROM Combined c
        INNER JOIN entitydefaults ed ON ed.definition = c.combined_def
        INNER JOIN #prod_costs apc  ON ed.definitionname = apc.product
        WHERE c.total_required_quantity > 0
          AND @remaining_rawmat_budget > 0;

        -- Step 5: Raw resource sell orders — price at cost * raw_mat_sell_multiplier
        INSERT INTO marketitems (
            marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
        )
        SELECT
            @marketeid,
            ed.definition,
            @vendoreid,
            0,
            1,
            apc.production_cost_nic * @raw_mat_sell_multiplier,
            10000000,
            1,
            1
        FROM #raw_materials rrm
        INNER JOIN entitydefaults ed ON rrm.raw_material = ed.definitionname
        INNER JOIN #prod_costs apc  ON rrm.raw_material  = apc.product
        GROUP BY ed.definition, apc.production_cost_nic;

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
        INNER JOIN #prod_costs pc    ON moc.definitionname = pc.product;

    END TRY
    BEGIN CATCH
        PRINT 'Error in usp_RefreshAutoMarketOrders: ' + ERROR_MESSAGE();
        THROW;
    END CATCH
END;
GO
```

- [ ] **Step 3.8: Commit**

```bash
git add docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql
git commit -m "feat(db): update usp_RefreshAutoMarketOrders — sell margins, rawmat budget cap, buyback orders (ISSUE-024)"
```

---

## Task 4: Add raw material purchase tracking to `Market.cs`

**Files:**
- Modify: `src/Perpetuum/Services/MarketEngine/Market.cs`

Three additions inside `FulfillSellOrderInstantly`. All three mirror the immediately preceding plasma recording block, using `cf_raw_material` instead of `cf_reactor_plasma`.

`isAutoOrder` is not loaded into `MarketOrder` — the `buyOrder.isVendorItem` guard is sufficient because only AutoMarket posts vendor buy orders for raw materials.

- [ ] **Step 4.1: Add recording at partial fulfillment branch (after line 790)**

In `Market.cs`, locate this exact block (lines 776–790):

```csharp
                    // Log plasma sold and income earned
                    if (itemToSell.ED.CategoryFlags.IsCategory(CategoryFlags.cf_reactor_plasma))
                    {
                        using (TransactionScope scope = Db.CreateTransaction())
                        {
                            _ = Db.Query()
                                .CommandText("exec sp_RecordPlasmaSold @sold_on, @plasma_type, @quantity, @income")
                                .SetParameter("@sold_on", DateTime.UtcNow)
                                .SetParameter("@plasma_type", itemToSell.ED.Name)
                                .SetParameter("@quantity", quantity)
                                .SetParameter("@income", buyOrder.price * quantity)
                                .ExecuteNonQuery();
                            scope.Complete();
                        }
                    }
```

Add the raw material block immediately after it (before `quantity = buyOrder.quantity;`):

```csharp
                    // Log plasma sold and income earned
                    if (itemToSell.ED.CategoryFlags.IsCategory(CategoryFlags.cf_reactor_plasma))
                    {
                        using (TransactionScope scope = Db.CreateTransaction())
                        {
                            _ = Db.Query()
                                .CommandText("exec sp_RecordPlasmaSold @sold_on, @plasma_type, @quantity, @income")
                                .SetParameter("@sold_on", DateTime.UtcNow)
                                .SetParameter("@plasma_type", itemToSell.ED.Name)
                                .SetParameter("@quantity", quantity)
                                .SetParameter("@income", buyOrder.price * quantity)
                                .ExecuteNonQuery();
                            scope.Complete();
                        }
                    }

                    // Log raw material AutoMarket purchase for daily budget tracking
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

- [ ] **Step 4.2: Add recording at post-finite block (after line 815, before `return`)**

Locate this exact block (lines 801–815):

```csharp
                // Log plasma sold and income earned
                if (itemToSell.ED.CategoryFlags.IsCategory(CategoryFlags.cf_reactor_plasma))
                {
                    using (TransactionScope scope = Db.CreateTransaction())
                    {
                        _ = Db.Query()
                            .CommandText("exec sp_RecordPlasmaSold @sold_on, @plasma_type, @quantity, @income")
                            .SetParameter("@sold_on", DateTime.UtcNow)
                            .SetParameter("@plasma_type", itemToSell.ED.Name)
                            .SetParameter("@quantity", quantity)
                            .SetParameter("@income", buyOrder.price * quantity)
                            .ExecuteNonQuery();
                        scope.Complete();
                    }
                }

                return;
```

Add the raw material block immediately before `return`:

```csharp
                // Log plasma sold and income earned
                if (itemToSell.ED.CategoryFlags.IsCategory(CategoryFlags.cf_reactor_plasma))
                {
                    using (TransactionScope scope = Db.CreateTransaction())
                    {
                        _ = Db.Query()
                            .CommandText("exec sp_RecordPlasmaSold @sold_on, @plasma_type, @quantity, @income")
                            .SetParameter("@sold_on", DateTime.UtcNow)
                            .SetParameter("@plasma_type", itemToSell.ED.Name)
                            .SetParameter("@quantity", quantity)
                            .SetParameter("@income", buyOrder.price * quantity)
                            .ExecuteNonQuery();
                        scope.Complete();
                    }
                }

                // Log raw material AutoMarket purchase for daily budget tracking
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

                return;
```

- [ ] **Step 4.3: Add recording at infinite vendor buy order path (after line 850)**

Locate this exact block (lines 836–850):

```csharp
            // Log plasma sold and income earned
            if (itemToSell.ED.CategoryFlags.IsCategory(CategoryFlags.cf_reactor_plasma))
            {
                using (TransactionScope scope = Db.CreateTransaction())
                {
                    _ = Db.Query()
                        .CommandText("exec sp_RecordPlasmaSold @sold_on, @plasma_type, @quantity, @income")
                        .SetParameter("@sold_on", DateTime.UtcNow)
                        .SetParameter("@plasma_type", itemToSell.ED.Name)
                        .SetParameter("@quantity", itemToSell.Quantity)
                        .SetParameter("@income", buyOrder.price * itemToSell.Quantity)
                        .ExecuteNonQuery();
                    scope.Complete();
                }
            }
        }
```

Add the raw material block before the closing `}` of the method:

```csharp
            // Log plasma sold and income earned
            if (itemToSell.ED.CategoryFlags.IsCategory(CategoryFlags.cf_reactor_plasma))
            {
                using (TransactionScope scope = Db.CreateTransaction())
                {
                    _ = Db.Query()
                        .CommandText("exec sp_RecordPlasmaSold @sold_on, @plasma_type, @quantity, @income")
                        .SetParameter("@sold_on", DateTime.UtcNow)
                        .SetParameter("@plasma_type", itemToSell.ED.Name)
                        .SetParameter("@quantity", itemToSell.Quantity)
                        .SetParameter("@income", buyOrder.price * itemToSell.Quantity)
                        .ExecuteNonQuery();
                    scope.Complete();
                }
            }

            // Log raw material AutoMarket purchase for daily budget tracking
            if (buyOrder.isVendorItem && itemToSell.ED.CategoryFlags.IsCategory(CategoryFlags.cf_raw_material))
            {
                using (TransactionScope scope = Db.CreateTransaction())
                {
                    _ = Db.Query()
                        .CommandText("exec sp_RecordRawMatPurchased @purchased_on, @item_def, @quantity, @income")
                        .SetParameter("@purchased_on", DateTime.UtcNow)
                        .SetParameter("@item_def", itemToSell.Definition)
                        .SetParameter("@quantity", itemToSell.Quantity)
                        .SetParameter("@income", buyOrder.price * itemToSell.Quantity)
                        .ExecuteNonQuery();
                    scope.Complete();
                }
            }
        }
```

- [ ] **Step 4.4: Build**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: `Build succeeded. 0 Error(s)` with no warnings in `Market.cs`.

- [ ] **Step 4.5: Commit**

```bash
git add src/Perpetuum/Services/MarketEngine/Market.cs
git commit -m "feat: track raw material AutoMarket purchases in Market.cs for daily NIC budget cap (ISSUE-024)"
```

---

## Task 5: End-to-end validation and backlog update

**Files:**
- Modify: `docs/backlog/issues.md`

- [ ] **Step 5.1: Confirm all config params are present**

```sql
SELECT param_name, param_value FROM automarket_config ORDER BY param_name;
-- Expected: 9 rows including all original IMPROVEMENT-030 params plus:
-- daily_rawmat_budget_nic = 5000000
-- product_buyback_margin  = 0.8
-- product_sell_margin     = 1.2
-- raw_mat_sell_multiplier = 1.5
```

- [ ] **Step 5.2: Run full refresh and verify all order types**

```sql
EXEC usp_RefreshAutoMarketOrders;

-- All auto order types
SELECT
    CASE mi.isSell WHEN 1 THEN 'sell' ELSE 'buy' END AS order_type,
    CASE
        WHEN ed.definitionname LIKE '%plasma%'                        THEN 'plasma'
        WHEN EXISTS (SELECT 1 FROM market_orders_configuration moc
                     WHERE moc.definitionname = ed.definitionname)    THEN 'production_item'
        ELSE 'raw_material'
    END AS item_class,
    COUNT(*) AS order_count,
    MIN(mi.price) AS min_price,
    MAX(mi.price) AS max_price
FROM marketitems mi
JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
WHERE mi.isAutoOrder = 1
GROUP BY mi.isSell,
    CASE
        WHEN ed.definitionname LIKE '%plasma%'                        THEN 'plasma'
        WHEN EXISTS (SELECT 1 FROM market_orders_configuration moc
                     WHERE moc.definitionname = ed.definitionname)    THEN 'production_item'
        ELSE 'raw_material'
    END
ORDER BY item_class, order_type;
-- Expected rows:
--   plasma / buy       — plasma buy orders exist, count > 0
--   production_item / sell — sell orders at 1.2× cost, count > 0
--   production_item / buy  — buyback orders at 0.80× cost, count > 0 (NEW)
--   raw_material / sell    — sell orders at 1.5× cost, count > 0
--   raw_material / buy     — buy orders at 1× cost, count > 0 (when budget > 0)
```

- [ ] **Step 5.3: Sell a raw material item to AutoMarket (requires running server)**

Have a player sell a raw material item (ore, mineral, liquid) to the AutoMarket buy order. After the transaction:

```sql
SELECT * FROM rawmat_purchased
WHERE purchased_on = CAST(GETUTCDATE() AS DATE);
-- Expected: a row with the sold item's definition, positive quantity, positive income
```

- [ ] **Step 5.4: Confirm no NULL production costs**

```sql
SELECT COUNT(*) FROM v_all_production_costs WHERE production_cost_nic IS NULL;
-- Expected: 0

SELECT COUNT(*) FROM v_all_production_costs WHERE production_cost_nic <= 0;
-- Expected: 0
```

- [ ] **Step 5.5: Update ISSUE-024 backlog status**

In `docs/backlog/issues.md`, change the ISSUE-024 entry:

```markdown
Status: DONE
```

- [ ] **Step 5.6: Commit backlog update**

```bash
git add docs/backlog/issues.md
git commit -m "docs: mark ISSUE-024 as DONE"
```

---

## Regression Checklist

Before considering complete, verify:

| Risk | Check |
|---|---|
| Product sell prices higher by 20% | Expected. Verify ratio ≈ 1.2 in Step 3.2. |
| Raw mat sell prices lower (2.0 → 1.5) | Expected positive change. `v_all_production_costs` uses `resource_market_prices` (not sell order prices) so production cost calculations are unaffected. |
| Buyback orders inflate `automarket_unbought_resources` | Verified by Step 3.5. |
| Plasma buy orders unchanged | No changes to Steps 1.1–1.3. Verify plasma orders still appear with correct quantities in Step 5.2. |
| `rawmat_purchased` MERGE race condition | Low risk; same `TransactionScope` pattern used for plasma without issues. |
| Build success | Verified in Step 4.4. |
