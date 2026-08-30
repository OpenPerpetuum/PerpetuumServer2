# IMPROVEMENT-030 AutoMarket Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the AutoMarket NIC faucet, introduce zone-aware gather tracking, replace static raw material prices with a dynamic supply/demand + PvP-risk formula, and set-base the cursor SQL for performance.

**Architecture:** Schema-first (tables → procs → views → C# → manager), so each layer can be validated before the next depends on it. SQL changes ship as both live ALTER statements and updated docs `.sql` files. No automated tests exist; each task ends with a manual validation query.

**Tech Stack:** SQL Server (T-SQL), C# 12 / .NET 8, existing `Db.Query()` pattern, `TimerAction` / `IProcess` pattern.

**Spec:** `docs/superpowers/specs/2026-05-27-automarket-overhaul-design.md`

---

## File Map

| File | Action |
|---|---|
| SQL Server (live DB) | CREATE TABLE `automarket_config`; ALTER TABLE `resources_gathered_daily`, `resources_gathered`; ALTER PROC ×3; ALTER VIEW ×1 |
| `docs/db_structure/database_schema_documentation.md` | Add `automarket_config` entry; add `is_pvp` column to two tables |
| `docs/db_structure/stored_procedures/dbo.sp_RecordResourceGathered.StoredProcedure.sql` | Add `@is_pvp` param |
| `docs/db_structure/stored_procedures/dbo.consolidate_statistics.StoredProcedure.sql` | Add `is_pvp` to GROUP BY and MERGE |
| `docs/db_structure/stored_procedures/dbo.recalculate_raw_material_prices.StoredProcedure.sql` | Complete rewrite |
| `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql` | Budget cap + set-based inserts |
| `docs/db_structure/views/v_all_production_costs.sql` | Remove `raw_material_prices` fallback |
| `src/Perpetuum/Modules/DrillerModule.cs` | Add `@is_pvp` at line 210 |
| `src/Perpetuum/Modules/HarvesterModule.cs` | Add `@is_pvp` at line 160 |
| `src/Perpetuum/Modules/LargeDrillerModule.cs` | Add `@is_pvp` at line 131 |
| `src/Perpetuum/Modules/LargeHarvesterModule.cs` | Add `@is_pvp` at line 102 |
| `src/Perpetuum/Services/Looting/LootContainer.cs` | Add `@is_pvp` at line 637 |
| `src/Perpetuum/Services/MarketEngine/MarketAutoOrdersManager.cs` | Change interval; evaluate async wrapping |

---

## Task 1: Create `automarket_config` table

**Files:**
- Live DB: new table
- `docs/db_structure/database_schema_documentation.md`: new entry

- [ ] **Step 1.1: Execute schema DDL in SQL Server Management Studio**

```sql
IF OBJECT_ID('dbo.automarket_config', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.automarket_config (
        param_name  VARCHAR(100) NOT NULL,
        param_value FLOAT        NOT NULL,
        CONSTRAINT PK_automarket_config PRIMARY KEY (param_name)
    );

    INSERT INTO dbo.automarket_config (param_name, param_value) VALUES
        ('plasma_anchor_fraction',  0.15),
        ('plasma_buy_qty_fraction', 0.60),
        ('daily_plasma_budget_nic', 500000),
        ('resource_ds_ratio_min',   0.25),
        ('resource_ds_ratio_max',   4.0);
END;
```

- [ ] **Step 1.2: Verify table and data**

```sql
SELECT param_name, param_value FROM automarket_config ORDER BY param_name;
-- Expected: 5 rows matching the values above
```

- [ ] **Step 1.3: Add entry to `docs/db_structure/database_schema_documentation.md`**

Find the alphabetically correct position (between `automarket_unbought_resources` and the next table). Add:

```markdown
## automarket_config

**Schema:** `dbo`

### Columns

| Column | Definition |
|---|---|
| `param_name` | `varchar(100) [not null, pk]` |
| `param_value` | `float [not null]` |

### Seeded rows

| param_name | param_value |
|---|---|
| `plasma_anchor_fraction` | `0.15` |
| `plasma_buy_qty_fraction` | `0.60` |
| `daily_plasma_budget_nic` | `500000` |
| `resource_ds_ratio_min` | `0.25` |
| `resource_ds_ratio_max` | `4.0` |

---
```

- [ ] **Step 1.4: Commit**

```bash
git add "docs/db_structure/database_schema_documentation.md"
git commit -m "feat(db): add automarket_config table (IMPROVEMENT-030)"
```

---

## Task 2: Add `is_pvp` to gather tables

**Files:**
- Live DB: ALTER TABLE × 2
- `docs/db_structure/database_schema_documentation.md`: update two table entries

- [ ] **Step 2.1: Execute DDL in SSMS**

```sql
-- resources_gathered_daily
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.resources_gathered_daily') AND name = 'is_pvp'
)
    ALTER TABLE dbo.resources_gathered_daily
        ADD is_pvp BIT NOT NULL DEFAULT 0;

-- resources_gathered (summary table)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.resources_gathered') AND name = 'is_pvp'
)
    ALTER TABLE dbo.resources_gathered
        ADD is_pvp BIT NOT NULL DEFAULT 0;
```

- [ ] **Step 2.2: Verify columns exist**

```sql
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('resources_gathered_daily', 'resources_gathered')
  AND COLUMN_NAME = 'is_pvp';
-- Expected: 2 rows, both BIT, NOT NULL, DEFAULT 0
```

- [ ] **Step 2.3: Verify existing rows got default value**

```sql
SELECT COUNT(*) AS total, SUM(CAST(is_pvp AS INT)) AS pvp_count
FROM resources_gathered;
-- Expected: pvp_count = 0 (all historical rows treated as PvE)
```

- [ ] **Step 2.4: Update schema documentation**

In `docs/db_structure/database_schema_documentation.md`, in the `resources_gathered_daily` and `resources_gathered` table entries, add:

```markdown
| `is_pvp` | `bit [not null, default: 0]` |
```

- [ ] **Step 2.5: Commit**

```bash
git add "docs/db_structure/database_schema_documentation.md"
git commit -m "feat(db): add is_pvp column to resources_gathered tables (IMPROVEMENT-030)"
```

---

## Task 3: Alter `sp_RecordResourceGathered`

**Files:**
- Live DB: ALTER PROCEDURE
- `docs/db_structure/stored_procedures/dbo.sp_RecordResourceGathered.StoredProcedure.sql`

- [ ] **Step 3.1: ALTER the procedure in SSMS**

```sql
ALTER PROCEDURE [dbo].[sp_RecordResourceGathered]
    @gathered_on   DATE,
    @resource_name VARCHAR(100),
    @quantity      BIGINT,
    @is_pvp        BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO resources_gathered_daily (gathered_on, resource_name, quantity, is_pvp)
    VALUES (@gathered_on, @resource_name, @quantity, @is_pvp);
END;
```

- [ ] **Step 3.2: Verify the parameter is accepted and backward-compatible**

```sql
-- Omitting @is_pvp (backward compat, default = 0)
EXEC sp_RecordResourceGathered @gathered_on = '2026-01-01', @resource_name = 'test_material', @quantity = 1;

-- With @is_pvp = 1 (PvP gather)
EXEC sp_RecordResourceGathered @gathered_on = '2026-01-01', @resource_name = 'test_material', @quantity = 2, @is_pvp = 1;

SELECT * FROM resources_gathered_daily WHERE resource_name = 'test_material';
-- Expected: 2 rows — one with is_pvp=0, one with is_pvp=1

-- Clean up test rows
DELETE FROM resources_gathered_daily WHERE resource_name = 'test_material';
```

- [ ] **Step 3.3: Update the doc file**

Replace the full content of `docs/db_structure/stored_procedures/dbo.sp_RecordResourceGathered.StoredProcedure.sql` with:

```sql
USE [perpetuumsa]
GO
/****** Object:  StoredProcedure [dbo].[sp_RecordResourceGathered]    Script Date: 28.05.2026 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

---- Register gathered resource quantity with optional PvP zone flag

CREATE PROCEDURE [dbo].[sp_RecordResourceGathered]
    @gathered_on   DATE,
    @resource_name VARCHAR(100),
    @quantity      BIGINT,
    @is_pvp        BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO resources_gathered_daily (gathered_on, resource_name, quantity, is_pvp)
    VALUES (@gathered_on, @resource_name, @quantity, @is_pvp);
END;
GO
```

- [ ] **Step 3.4: Commit**

```bash
git add "docs/db_structure/stored_procedures/dbo.sp_RecordResourceGathered.StoredProcedure.sql"
git commit -m "feat(db): add @is_pvp param to sp_RecordResourceGathered (IMPROVEMENT-030)"
```

---

## Task 4: Alter `consolidate_statistics`

**Files:**
- Live DB: ALTER PROCEDURE
- `docs/db_structure/stored_procedures/dbo.consolidate_statistics.StoredProcedure.sql`

- [ ] **Step 4.1: ALTER the procedure in SSMS**

Only the resources block changes. The plasma block is unchanged.

```sql
ALTER PROCEDURE [dbo].[consolidate_statistics]
AS
BEGIN
    SET NOCOUNT ON;

    -- Resources block: aggregate daily buffer into summary, tracking is_pvp
    WITH Aggregated AS (
        SELECT
            gathered_on,
            resource_name,
            is_pvp,
            SUM(quantity) AS total_quantity
        FROM resources_gathered_daily WITH (READPAST)
        GROUP BY gathered_on, resource_name, is_pvp
    )
    MERGE INTO resources_gathered AS target
    USING Aggregated AS source
    ON  target.gathered_on   = source.gathered_on
    AND target.resource_name = source.resource_name
    AND target.is_pvp        = source.is_pvp
    WHEN MATCHED THEN
        UPDATE SET quantity = target.quantity + source.total_quantity
    WHEN NOT MATCHED THEN
        INSERT (gathered_on, resource_name, quantity, is_pvp)
        VALUES (source.gathered_on, source.resource_name, source.total_quantity, source.is_pvp);

    DELETE FROM resources_gathered_daily;

    -- Plasma block: unchanged
    WITH Aggregated AS (
        SELECT
            gathered_on,
            plasma_type,
            SUM(quantity) AS total_quantity
        FROM plasma_gathered_daily WITH (READPAST)
        GROUP BY gathered_on, plasma_type
    )
    MERGE INTO plasma_gathered AS target
    USING Aggregated AS source
    ON  target.gathered_on = source.gathered_on
    AND target.plasma_type = source.plasma_type
    WHEN MATCHED THEN
        UPDATE SET quantity = target.quantity + source.total_quantity
    WHEN NOT MATCHED THEN
        INSERT (gathered_on, plasma_type, quantity)
        VALUES (source.gathered_on, source.plasma_type, source.total_quantity);

    DELETE FROM plasma_gathered_daily;
END;
GO
```

- [ ] **Step 4.2: Verify the MERGE key change**

Seed test rows with both PvP and PvE for the same resource on the same day:

```sql
INSERT INTO resources_gathered_daily (gathered_on, resource_name, quantity, is_pvp)
VALUES ('2026-01-02', 'test_ore', 100, 0),
       ('2026-01-02', 'test_ore', 200, 1);

EXEC consolidate_statistics;

SELECT gathered_on, resource_name, quantity, is_pvp
FROM resources_gathered WHERE resource_name = 'test_ore';
-- Expected: 2 rows — quantity=100 is_pvp=0, quantity=200 is_pvp=1

-- Clean up
DELETE FROM resources_gathered WHERE resource_name = 'test_ore';
```

- [ ] **Step 4.3: Update the doc file**

Replace the full content of `docs/db_structure/stored_procedures/dbo.consolidate_statistics.StoredProcedure.sql` with the procedure text from Step 4.1 (wrapped in `USE [perpetuumsa] GO` header as the existing file uses).

- [ ] **Step 4.4: Commit**

```bash
git add "docs/db_structure/stored_procedures/dbo.consolidate_statistics.StoredProcedure.sql"
git commit -m "feat(db): include is_pvp in consolidate_statistics MERGE key (IMPROVEMENT-030)"
```

---

## Task 5: Update C# gather call sites to pass `@is_pvp`

**Files:**
- `src/Perpetuum/Modules/DrillerModule.cs` (line 210)
- `src/Perpetuum/Modules/HarvesterModule.cs` (line 160)
- `src/Perpetuum/Modules/LargeDrillerModule.cs` (line 131)
- `src/Perpetuum/Modules/LargeHarvesterModule.cs` (line 102)
- `src/Perpetuum/Services/Looting/LootContainer.cs` (line 637)

`zone.Configuration.Protected == true` means alpha (PvE zone) → `@is_pvp = false`.  
`zone.Configuration.Protected == false` means beta/gamma (PvP zone) → `@is_pvp = true`.

- [ ] **Step 5.1: Update `DrillerModule.cs` (lines 209–214)**

Old:
```csharp
Db.Query()
    .CommandText("exec sp_RecordResourceGathered @gathered_on, @resource_name, @quantity")
    .SetParameter("@gathered_on", DateTime.UtcNow)
    .SetParameter("@resource_name", resourceName)
    .SetParameter("@quantity", quantity)
    .ExecuteNonQuery();
```

New:
```csharp
Db.Query()
    .CommandText("exec sp_RecordResourceGathered @gathered_on, @resource_name, @quantity, @is_pvp")
    .SetParameter("@gathered_on", DateTime.UtcNow)
    .SetParameter("@resource_name", resourceName)
    .SetParameter("@quantity", quantity)
    .SetParameter("@is_pvp", !zone.Configuration.Protected)
    .ExecuteNonQuery();
```

- [ ] **Step 5.2: Update `HarvesterModule.cs` (lines 159–164)**

Apply the identical change as Step 5.1 — same pattern, same `zone` variable in scope.

- [ ] **Step 5.3: Update `LargeDrillerModule.cs` (lines 130–135)**

Apply the identical change as Step 5.1.

- [ ] **Step 5.4: Update `LargeHarvesterModule.cs` (lines 101–106)**

Apply the identical change as Step 5.1.

- [ ] **Step 5.5: Update `LootContainer.cs` (lines 636–641)**

The `zone` parameter is in scope at this location — the enclosing method is `Build(IZone zone, Position position)`.

Old:
```csharp
Db.Query()
    .CommandText("exec sp_RecordResourceGathered @gathered_on, @resource_name, @quantity")
    .SetParameter("@gathered_on", DateTime.UtcNow)
    .SetParameter("@resource_name", fragment.Key)
    .SetParameter("@quantity", fragment.Sum(x => x.ItemInfo.Quantity))
    .ExecuteNonQuery();
```

New:
```csharp
Db.Query()
    .CommandText("exec sp_RecordResourceGathered @gathered_on, @resource_name, @quantity, @is_pvp")
    .SetParameter("@gathered_on", DateTime.UtcNow)
    .SetParameter("@resource_name", fragment.Key)
    .SetParameter("@quantity", fragment.Sum(x => x.ItemInfo.Quantity))
    .SetParameter("@is_pvp", !zone.Configuration.Protected)
    .ExecuteNonQuery();
```

- [ ] **Step 5.6: Build to confirm no compilation errors**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: Build succeeded, 0 errors, 0 warnings in modified files.

- [ ] **Step 5.7: Commit**

```bash
git add src/Perpetuum/Modules/DrillerModule.cs
git add src/Perpetuum/Modules/HarvesterModule.cs
git add src/Perpetuum/Modules/LargeDrillerModule.cs
git add src/Perpetuum/Modules/LargeHarvesterModule.cs
git add src/Perpetuum/Services/Looting/LootContainer.cs
git commit -m "feat: pass @is_pvp to sp_RecordResourceGathered from all gather modules (IMPROVEMENT-030)"
```

---

## Task 6: Rewrite `recalculate_raw_material_prices`

**Files:**
- Live DB: ALTER PROCEDURE
- `docs/db_structure/stored_procedures/dbo.recalculate_raw_material_prices.StoredProcedure.sql`

**New formula:**
```
plasma_anchor = alpha_common_plasma_price × plasma_anchor_fraction
supply_daily_avg = SUM(resources_gathered.quantity WHERE last 7 days) / 7.0
demand_daily_avg = SUM(v_required_raw_materials.total_quantity) / 7.0
ds_ratio = CLAMP(ds_ratio_min, ds_ratio_max, demand / supply_daily_avg)
pvp_ratio = pvp_qty / total_qty   (NULL if no gather data)
risk = 1.0 + ISNULL(pvp_ratio, 1.0)
price = ROUND(plasma_anchor × ds_ratio × risk, 2)
```

Zero or no gather data → `ds_ratio` hits ceiling (4.0), `risk` = 2.0 → max-scarcity price (correct).

- [ ] **Step 6.1: ALTER the procedure in SSMS**

```sql
ALTER PROCEDURE [dbo].[recalculate_raw_material_prices]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @today      DATE  = CAST(GETUTCDATE() AS DATE);
    DECLARE @week_start DATE  = DATEADD(DAY, -DATEPART(WEEKDAY, @today) + 2, @today);
    DECLARE @start_date DATE  = DATEADD(DAY, -7, @today);

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
            SUM(CASE WHEN is_pvp = 0 THEN quantity ELSE 0 END)  AS pve_qty,
            SUM(CASE WHEN is_pvp = 1 THEN quantity ELSE 0 END)  AS pvp_qty,
            SUM(quantity)                                         AS total_qty,
            SUM(quantity) / 7.0                                   AS supply_daily_avg
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
                            WHEN d.daily_demand / s.supply_daily_avg < @ds_min THEN @ds_min
                            WHEN d.daily_demand / s.supply_daily_avg > @ds_max THEN @ds_max
                            ELSE d.daily_demand / s.supply_daily_avg
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
    DELETE FROM plasma_gathered    WHERE gathered_on < DATEADD(DAY, -90, @today);
    DELETE FROM plasma_sold        WHERE sold_on     < DATEADD(DAY, -90, @today);
    DELETE FROM resources_gathered WHERE gathered_on < DATEADD(DAY, -90, @today);
END;
GO
```

- [ ] **Step 6.2: Verify output manually**

```sql
EXEC recalculate_raw_material_prices;

-- All materials in the production chain should have a price
SELECT r.resource_name, r.unit_price
FROM resource_market_prices r
WHERE r.calculated_on = (SELECT MAX(calculated_on) FROM resource_market_prices)
ORDER BY r.unit_price DESC;
-- Expected: one row per raw material in v_required_raw_materials
-- No NULL prices, no zero prices

-- Confirm formula range: prices should be between anchor×0.25×1 and anchor×4×2
-- With alpha common plasma at its current dynamic price × 0.15 = anchor
-- Min possible: anchor × 0.25 × 1.0
-- Max possible: anchor × 4.0 × 2.0
DECLARE @anchor FLOAT = (
    SELECT TOP 1 dynamic_price * 0.15
    FROM fn_CalculateDynamicPlasmaPrices(1)
    WHERE plasma_type = 'def_common_reactor_plasma'
);
SELECT @anchor * 0.25 AS min_price, @anchor * 8.0 AS max_price;
-- Verify the prices in resource_market_prices fall within this range
```

- [ ] **Step 6.3: Confirm PvP materials price higher than equivalent PvE**

If the DB has gather history with `is_pvp` data, run:

```sql
SELECT resource_name, unit_price
FROM resource_market_prices
WHERE calculated_on = (SELECT MAX(calculated_on) FROM resource_market_prices)
ORDER BY unit_price DESC;
-- Materials exclusively from PvP zones should appear higher than PvE equivalents
-- with matching supply/demand ratios (price = anchor × ds_ratio × 2.0 vs × 1.0)
```

If no `is_pvp=1` gather history exists yet, confirm materials with no gather data at all get the max-scarcity price (~anchor × 8.0).

- [ ] **Step 6.4: Update the doc file**

Replace the full content of `docs/db_structure/stored_procedures/dbo.recalculate_raw_material_prices.StoredProcedure.sql` with the procedure text from Step 6.1 (wrapped in standard `USE [perpetuumsa] GO` header).

- [ ] **Step 6.5: Commit**

```bash
git add "docs/db_structure/stored_procedures/dbo.recalculate_raw_material_prices.StoredProcedure.sql"
git commit -m "feat(db): rewrite recalculate_raw_material_prices with supply/demand + PvP-risk formula (IMPROVEMENT-030)"
```

---

## Task 7: Alter `v_all_production_costs`

**Files:**
- Live DB: ALTER VIEW
- `docs/db_structure/views/v_all_production_costs.sql`

Remove the `raw_material_prices` dependency. The `raw_resources` CTE switches from reading `raw_material_prices` for both enumeration and fallback price to reading from `v_required_raw_materials` + `resource_market_prices`. An inline max-scarcity fallback replaces `base.price_nic`.

- [ ] **Step 7.1: Execute ALTER VIEW in SSMS**

```sql
ALTER VIEW [dbo].[v_all_production_costs] AS
WITH all_items AS (
    SELECT product AS item FROM production_data
    UNION
    SELECT components AS item FROM production_data
),
recursive_materials AS (
    SELECT 
        base.item,
        pd.components AS raw_material,
        CAST(pd.amount * 2.1 AS FLOAT) AS quantity
    FROM all_items base
    JOIN production_data pd ON pd.product = base.item

    UNION ALL

    SELECT
        rm.item,
        pd.components AS raw_material,
        rm.quantity * pd.amount * 2.1 AS quantity
    FROM recursive_materials rm
    JOIN production_data pd ON rm.raw_material = pd.product
),
aggregated_costs AS (
    SELECT
        rm.item AS product,
        rm.raw_material,
        SUM(rm.quantity) AS total_quantity
    FROM recursive_materials rm
    GROUP BY rm.item, rm.raw_material
),
latest_market_prices AS (
    SELECT rmp.resource_name, rmp.unit_price
    FROM resource_market_prices rmp
    WHERE rmp.calculated_on = (SELECT MAX(calculated_on) FROM resource_market_prices)
),
-- Inline max-scarcity fallback: plasma_anchor × ds_ratio_max × 2.0
-- Used when a material is completely absent from resource_market_prices
max_scarcity_price AS (
    SELECT TOP 1
        cdp.dynamic_price
        * (SELECT param_value FROM automarket_config WHERE param_name = 'plasma_anchor_fraction')
        * (SELECT param_value FROM automarket_config WHERE param_name = 'resource_ds_ratio_max')
        * 2.0 AS price
    FROM fn_CalculateDynamicPlasmaPrices(1) cdp
    WHERE cdp.plasma_type = 'def_common_reactor_plasma'
),
computed_costs AS (
    SELECT
        ac.product,
        SUM(
            ac.total_quantity * ISNULL(mp.unit_price, (SELECT price FROM max_scarcity_price))
        ) AS production_cost_nic
    FROM aggregated_costs ac
    LEFT JOIN latest_market_prices mp 
        ON ac.raw_material COLLATE DATABASE_DEFAULT = mp.resource_name COLLATE DATABASE_DEFAULT
    GROUP BY ac.product
),
raw_resources AS (
    SELECT 
        base.raw_material AS product,
        ISNULL(mp.unit_price, (SELECT price FROM max_scarcity_price)) AS production_cost_nic
    FROM (SELECT DISTINCT raw_material FROM v_required_raw_materials) base
    LEFT JOIN latest_market_prices mp 
        ON base.raw_material COLLATE DATABASE_DEFAULT = mp.resource_name COLLATE DATABASE_DEFAULT
),
final_costs AS (
    SELECT * FROM computed_costs
    UNION
    SELECT * FROM raw_resources
)
SELECT 
    product,
    ROUND(production_cost_nic, 2) AS production_cost_nic
FROM final_costs;
GO
```

- [ ] **Step 7.2: Verify the view returns data**

```sql
SELECT TOP 20 product, production_cost_nic
FROM v_all_production_costs
ORDER BY production_cost_nic DESC;
-- Expected: no NULL production_cost_nic values
-- Prices should be positive and in a plausible range
```

- [ ] **Step 7.3: Confirm no raw_material_prices dependency**

```sql
-- Check the view definition no longer references raw_material_prices
SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.v_all_production_costs'));
-- Expected: 'raw_material_prices' does NOT appear in the output
```

- [ ] **Step 7.4: Update the doc file**

Replace the full content of `docs/db_structure/views/v_all_production_costs.sql` with the view text from Step 7.1 (wrapped in standard header with `SET ANSI_NULLS ON GO SET QUOTED_IDENTIFIER ON GO`).

- [ ] **Step 7.5: Commit**

```bash
git add "docs/db_structure/views/v_all_production_costs.sql"
git commit -m "feat(db): remove raw_material_prices dependency from v_all_production_costs (IMPROVEMENT-030)"
```

---

## Task 8: Rewrite `usp_RefreshAutoMarketOrders`

**Files:**
- Live DB: ALTER PROCEDURE
- `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql`

**Changes:**
1. Before plasma buy order inserts: read budget params and compute remaining daily budget.
2. Replace three cursor loops (alpha/beta/gamma) with set-based INSERTs that apply fractional quantity and budget cap.
3. Replace raw material buy order cursor (Step 4) with set-based INSERT.
4. Replace raw resource sell order cursor (Step 5) with set-based INSERT.

**Budget semantics:** `@remaining_budget` is computed once at the start using `plasma_sold.income` for today. Each individual plasma order's quantity is capped independently to `@remaining_budget / unit_price`. This is equivalent to the cursor approach (which also never decremented `@remaining_budget` inside the loop).

- [ ] **Step 8.1: Execute ALTER PROCEDURE in SSMS**

```sql
ALTER PROCEDURE [dbo].[usp_RefreshAutoMarketOrders]
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

        -- Unbought mats excluding plasma (definitions 3271-3274)
        INSERT INTO automarket_unbought_resources (itemdefinition, quantity)
        SELECT itemdefinition, SUM(CAST(quantity AS BIGINT))
        FROM marketitems
        WHERE isAutoOrder = 1 AND isSell = 0
          AND itemdefinition NOT IN (3271, 3272, 3273, 3274)
        GROUP BY itemdefinition;

        -- Step 1: Remove old auto orders
        DELETE FROM marketitems WHERE isAutoOrder = 1;

        -- Budget params for plasma buy orders
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

        -- Step 3: Product auto sell orders (unchanged, already set-based)
        INSERT INTO marketitems (
            marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
        )
        SELECT
            @marketeid,
            ed.definition,
            @vendoreid,
            0,
            1,
            pc.production_cost_nic,
            moc.amount,
            1,
            1
        FROM market_orders_configuration moc
        INNER JOIN entitydefaults ed ON moc.definitionname = ed.definitionname
        INNER JOIN v_all_production_costs pc ON moc.definitionname = pc.product;

        -- Step 4: Raw material buy orders (set-based, replaces cursor)
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
            INNER JOIN v_required_raw_materials rm ON rm.product = np.product
            INNER JOIN entitydefaults ed ON ed.definitionname = rm.raw_material
            WHERE np.need_amount > 0
            GROUP BY ed.definition
        ),
        Unbought AS (
            SELECT
                ub.itemdefinition AS raw_material_def,
                SUM(ub.quantity) AS required_from_unbought
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
        INNER JOIN v_all_production_costs apc ON ed.definitionname = apc.product
        WHERE c.total_required_quantity > 0;

        -- Step 5: Raw resource sell orders (set-based, replaces cursor)
        INSERT INTO marketitems (
            marketeid, itemdefinition, submittereid, duration, isSell, price, quantity, isvendoritem, isAutoorder
        )
        SELECT
            @marketeid,
            ed.definition,
            @vendoreid,
            0,
            1,
            apc.production_cost_nic * 2.0,
            10000000,
            1,
            1
        FROM v_required_raw_materials rrm
        INNER JOIN entitydefaults ed ON rrm.raw_material = ed.definitionname
        INNER JOIN v_all_production_costs apc ON rrm.raw_material = apc.product
        GROUP BY ed.definition, apc.production_cost_nic;

    END TRY
    BEGIN CATCH
        PRINT 'Error in usp_RefreshAutoMarketOrders: ' + ERROR_MESSAGE();
        THROW;
    END CATCH
END;
GO
```

- [ ] **Step 8.2: Test the procedure manually**

```sql
-- First, check current plasma budget spent today
SELECT sold_on, SUM(income) AS total_income
FROM plasma_sold
WHERE sold_on = CAST(GETUTCDATE() AS DATE)
GROUP BY sold_on;

-- Execute the refresh
EXEC usp_RefreshAutoMarketOrders;

-- Verify plasma buy orders were placed with reduced quantity
SELECT mi.itemdefinition, ed.definitionname, mi.price, mi.quantity, mi.isSell
FROM marketitems mi
JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
WHERE mi.isAutoOrder = 1 AND mi.isSell = 0
  AND ed.definitionname LIKE '%plasma%'
ORDER BY mi.marketeid, ed.definitionname;
-- Expected: plasma buy orders with quantity ≤ 60% of what the old procedure placed

-- Verify no NULL prices in auto orders
SELECT COUNT(*) FROM marketitems WHERE isAutoOrder = 1 AND price IS NULL;
-- Expected: 0

-- Verify raw material buy orders exist
SELECT COUNT(*) FROM marketitems WHERE isAutoOrder = 1 AND isSell = 0;
-- Expected: > 0
```

- [ ] **Step 8.3: Test budget cap**

```sql
-- Temporarily set a tiny budget to confirm orders are skipped
UPDATE automarket_config SET param_value = 1 WHERE param_name = 'daily_plasma_budget_nic';

EXEC usp_RefreshAutoMarketOrders;

SELECT COUNT(*) AS plasma_buy_orders
FROM marketitems mi
JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
WHERE mi.isAutoOrder = 1 AND mi.isSell = 0
  AND ed.definitionname LIKE '%plasma%';
-- Expected: 0 (budget is 1 NIC, prices are hundreds of NIC — nothing fits)

-- Restore the budget
UPDATE automarket_config SET param_value = 500000 WHERE param_name = 'daily_plasma_budget_nic';
EXEC usp_RefreshAutoMarketOrders;
```

- [ ] **Step 8.4: Update the doc file**

Replace the full content of `docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql` with the procedure text from Step 8.1 (wrapped in standard `USE [perpetuumsa] GO` header).

- [ ] **Step 8.5: Commit**

```bash
git add "docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql"
git commit -m "feat(db): add budget cap, fractional qty, set-based inserts to usp_RefreshAutoMarketOrders (IMPROVEMENT-030)"
```

---

## Task 9: Update `MarketAutoOrdersManager` — interval and thread-safety

**Files:**
- `src/Perpetuum/Services/MarketEngine/MarketAutoOrdersManager.cs`

### Part A — Change refresh interval

- [ ] **Step 9.1: Change `TimeSpan.FromDays(3)` to `TimeSpan.FromDays(1)`**

In `MarketAutoOrdersManager.cs`, in the `Init()` method, line 30:

Old:
```csharp
_timers.Add(new TimerAction(RecalculatePricesAndRenewOrders, TimeSpan.FromDays(3)));
```

New:
```csharp
_timers.Add(new TimerAction(RecalculatePricesAndRenewOrders, TimeSpan.FromDays(1)));
```

### Part B — Analyze and address thread-safety

- [ ] **Step 9.2: Read `TimerAction` and `TimerList` implementations**

Locate and read:

```bash
# Find the timer implementations
grep -r "class TimerAction" src/ --include="*.cs" -l
grep -r "class TimerList" src/ --include="*.cs" -l
```

Confirm: does `TimerList.Update(TimeSpan)` fire callbacks synchronously on the calling thread?

- [ ] **Step 9.3: Determine which thread drives `IProcess.Update`**

Read `ProcessManager` (or equivalent class that calls `.Update(time)` on registered processes):

```bash
grep -r "IProcess" src/ --include="*.cs" -l
grep -r "MarketAutoOrdersManager" src/ --include="*.cs" -l
```

Determine: is `Update(time)` called from the main server process loop (same thread as zone updates)?

- [ ] **Step 9.4: Apply async wrapping if warranted**

**Decision criteria:** If timer callbacks fire synchronously on the main process loop thread AND `RecalculatePricesAndRenewOrders` takes > 200 ms (estimated from: delete all auto orders + run price calc + re-insert all orders), wrap in `Task.Run` with exception logging.

If wrapping is warranted, apply this pattern to `RecalculatePricesAndRenewOrders` and `ConsolidateStatistics`:

```csharp
private void Init()
{
    _timers.Add(new TimerAction(ConsolidateStatisticsAsync, TimeSpan.FromMinutes(15)));
    _timers.Add(new TimerAction(RecalculatePricesAndRenewOrdersAsync, TimeSpan.FromDays(1)));
}

private void ConsolidateStatisticsAsync()
{
    Task.Run(() =>
    {
        try { ConsolidateStatistics(); }
        catch (Exception ex) { Logger.Error($"ConsolidateStatistics failed: {ex.Message}"); }
    });
}

private void RecalculatePricesAndRenewOrdersAsync()
{
    Task.Run(() =>
    {
        try { RecalculatePricesAndRenewOrders(); }
        catch (Exception ex) { Logger.Error($"RecalculatePricesAndRenewOrders failed: {ex.Message}"); }
    });
}

private void ConsolidateStatistics() { /* unchanged */ }
private void RecalculatePricesAndRenewOrders() { /* unchanged */ }
```

**Note:** `Logger` usage — find the Logger import in the file or a sibling class in the `MarketEngine` namespace and use the same pattern. If `Logger` is not available, use `System.Diagnostics.Debug.WriteLine` as a fallback and note it as technical debt.

If the analysis shows `Update` is NOT on the main process loop or the operations are fast enough, document the finding in a code comment and leave the synchronous approach. Either outcome must be committed with a note.

- [ ] **Step 9.5: Build**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors.

- [ ] **Step 9.6: Commit**

```bash
git add src/Perpetuum/Services/MarketEngine/MarketAutoOrdersManager.cs
git commit -m "feat: change AutoMarket refresh interval to 1 day; wrap async if process-loop (IMPROVEMENT-030)"
```

---

## Task 10: Update schema documentation

**Files:**
- `docs/db_structure/database_schema_documentation.md`

This task ensures the schema docs reflect all changes from Tasks 1–9 that haven't already been committed.

- [ ] **Step 10.1: Verify all four doc-file updates were committed**

Check that the following commits exist in git log:
- `automarket_config` table entry added
- `is_pvp` column added to `resources_gathered_daily` and `resources_gathered` entries
- `sp_RecordResourceGathered.StoredProcedure.sql` updated
- `consolidate_statistics.StoredProcedure.sql` updated
- `recalculate_raw_material_prices.StoredProcedure.sql` updated
- `usp_RefreshAutoMarketOrders.StoredProcedure.sql` updated
- `v_all_production_costs.sql` updated

```bash
git log --oneline | head -20
```

- [ ] **Step 10.2: Verify `raw_material_prices` is documented as deprecated**

In `docs/db_structure/database_schema_documentation.md`, find the `raw_material_prices` entry and add a deprecation note:

```markdown
> **Deprecated (IMPROVEMENT-030):** This table is no longer read by any active query path. Rows are retained as historical reference. Do not add new query dependencies on this table.
```

- [ ] **Step 10.3: Commit**

```bash
git add "docs/db_structure/database_schema_documentation.md"
git commit -m "docs: mark raw_material_prices as deprecated (IMPROVEMENT-030)"
```

---

## Task 11: End-to-end manual validation

All changes deployed. Run the full validation sequence from the spec.

- [ ] **Step 11.1: Confirm `automarket_config` is present and readable**

```sql
SELECT * FROM automarket_config;
-- Expected: 5 rows with correct values
```

- [ ] **Step 11.2: Run full market refresh and confirm plasma buy orders**

```sql
EXEC usp_RefreshAutoMarketOrders;

SELECT mi.marketeid, ed.definitionname, mi.price, mi.quantity
FROM marketitems mi
JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
WHERE mi.isAutoOrder = 1 AND mi.isSell = 0
  AND ed.definitionname LIKE '%plasma%'
ORDER BY mi.marketeid;

-- Verify: quantity for each order ≤ 60% of the last-7-day gathered for that plasma type
-- Cross-reference against:
SELECT plasma_type, SUM(quantity) AS gathered_7d
FROM plasma_gathered
WHERE gathered_on >= DATEADD(DAY, -7, CAST(GETUTCDATE() AS DATE))
GROUP BY plasma_type;
```

- [ ] **Step 11.3: Validate gather tracking captures PvP flag**

Requires a running server. Gather a small amount of resources in an alpha zone (PvE) and a beta zone (PvP). Wait ≤ 15 minutes for `consolidate_statistics` to run. Then:

```sql
SELECT gathered_on, resource_name, quantity, is_pvp
FROM resources_gathered
WHERE gathered_on = CAST(GETUTCDATE() AS DATE)
ORDER BY resource_name, is_pvp;
-- Expected: same resource appears with is_pvp=0 (alpha) and is_pvp=1 (beta)
```

- [ ] **Step 11.4: Validate dynamic raw material prices**

```sql
EXEC recalculate_raw_material_prices;

SELECT r.resource_name, r.unit_price
FROM resource_market_prices r
WHERE r.calculated_on = (SELECT MAX(calculated_on) FROM resource_market_prices)
ORDER BY r.unit_price DESC;
-- Expected: all materials in v_required_raw_materials have a price
-- No NULL, no zero prices
```

- [ ] **Step 11.5: Validate `v_all_production_costs` — no NULL production costs**

```sql
SELECT COUNT(*) FROM v_all_production_costs WHERE production_cost_nic IS NULL;
-- Expected: 0

SELECT COUNT(*) FROM v_all_production_costs WHERE production_cost_nic <= 0;
-- Expected: 0
```

- [ ] **Step 11.6: Validate item sell prices are in a reasonable range**

After `usp_RefreshAutoMarketOrders` runs, check auto sell order prices:

```sql
SELECT mi.price, ed.definitionname
FROM marketitems mi
JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
WHERE mi.isAutoOrder = 1 AND mi.isSell = 1
ORDER BY mi.price DESC;
-- Manually confirm prices look plausible (not 100× off from expected)
```

- [ ] **Step 11.7: Final build check**

```bash
dotnet build PerpetuumServer2.sln -c Release -p:Platform=x64
```

Expected: 0 errors, 0 warnings in any modified file.

- [ ] **Step 11.8: Update IMPROVEMENT-030 in backlog**

In `docs/backlog/improvements.md`, change the IMPROVEMENT-030 entry status from `IN_PROGRESS` to `DONE`.

- [ ] **Step 11.9: Commit backlog update**

```bash
git add "docs/backlog/improvements.md"
git commit -m "docs: mark IMPROVEMENT-030 as DONE"
```

---

## Regression Checklist

Before considering IMPROVEMENT-030 complete, verify:

| Risk | Check |
|---|---|
| Client market order visibility | Auto orders re-insert on refresh; no client IDs are persisted → no regression |
| Production cost calculations | Run Step 11.5 and 11.6 to confirm `v_all_production_costs` has no NULLs and sell prices are sane |
| `consolidate_statistics` key change | Historical rows default to `is_pvp = 0` (PvE) — correct; no data loss |
| Modules missing `@is_pvp` | Verify all 5 call sites were updated in Task 5; stored proc defaults to `0` (PvE) if missed |
| Daily budget exhaustion | Step 8.3 confirms zero plasma orders when budget is 0 |
| `v_all_production_costs` `raw_material_prices` removal | Step 7.3 confirms no reference remains |
