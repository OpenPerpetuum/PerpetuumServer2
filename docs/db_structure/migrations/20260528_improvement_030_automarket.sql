-- IMPROVEMENT-030: AutoMarket Overhaul — NIC Injection Control, Dynamic Pricing, Performance Refactor
-- Adds is_pvp to resources_gathered and resources_gathered_daily (Part B).
-- Creates automarket_config key-value table with default parameters (Part A).
--
-- Apply stored procedure and view changes separately by running the updated
-- .sql files in docs/db_structure/stored_procedures/ and docs/db_structure/views/
-- in this order: sp_RecordResourceGathered, consolidate_statistics,
-- recalculate_raw_material_prices, usp_RefreshAutoMarketOrders, v_all_production_costs.

BEGIN TRANSACTION;

-- 1. Add is_pvp to resources_gathered
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'resources_gathered' AND COLUMN_NAME = 'is_pvp'
)
BEGIN
    ALTER TABLE dbo.resources_gathered
        ADD is_pvp BIT NOT NULL DEFAULT 0;
END

-- 2. Add is_pvp to resources_gathered_daily
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'resources_gathered_daily' AND COLUMN_NAME = 'is_pvp'
)
BEGIN
    ALTER TABLE dbo.resources_gathered_daily
        ADD is_pvp BIT NOT NULL DEFAULT 0;
END

-- 3. Create automarket_config
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'automarket_config'
)
BEGIN
    CREATE TABLE dbo.automarket_config (
        param_name  VARCHAR(100) NOT NULL,
        param_value FLOAT        NOT NULL,
        CONSTRAINT PK_automarket_config PRIMARY KEY (param_name)
    );
END

-- 4. Seed default parameters (idempotent)
MERGE INTO dbo.automarket_config AS target
USING (VALUES
    ('plasma_anchor_fraction',  0.15),
    ('plasma_buy_qty_fraction',  0.60),
    ('daily_plasma_budget_nic',  500000),
    ('resource_ds_ratio_min',    0.25),
    ('resource_ds_ratio_max',    4.0)
) AS src (param_name, param_value)
ON target.param_name = src.param_name
WHEN NOT MATCHED THEN
    INSERT (param_name, param_value) VALUES (src.param_name, src.param_value);

COMMIT;
