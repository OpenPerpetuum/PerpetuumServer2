-- docs/db_structure/migrations/IMPROVEMENT-040-rawmat-decoupling.sql
-- IMPROVEMENT-040: AutoMarket Raw Material Decoupling
-- Run against perpetuumsa while server is ONLINE (no data migration needed).
-- Apply in order — objects are dependencies of later steps.
-- DEPLOY ORDER: After running this migration, ALSO apply (in order):
--   1. docs/db_structure/views/v_trade_list_raw_material_demand.sql (CREATE OR ALTER VIEW)
--   2. docs/db_structure/views/v_all_production_costs.sql (CREATE OR ALTER VIEW)
--   3. docs/db_structure/stored_procedures/dbo.recalculate_raw_material_prices.StoredProcedure.sql
--   4. docs/db_structure/stored_procedures/dbo.usp_RefreshAutoMarketOrders.StoredProcedure.sql
--   5. docs/db_structure/stored_procedures/dbo.sp_RecordRawMatWeeklyPurchased.StoredProcedure.sql

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
IF NOT EXISTS (SELECT 1 FROM dbo.automarket_config WHERE param_name = 'weekly_rawmat_cap_default')
BEGIN
    INSERT INTO dbo.automarket_config (param_name, param_value)
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
CREATE OR ALTER PROCEDURE [dbo].[sp_RecordRawMatWeeklyPurchased]
    @week_start     DATE,
    @definitionname VARCHAR(100),
    @quantity       BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    MERGE dbo.automarket_rawmat_weekly_tracking WITH (HOLDLOCK) AS target
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
