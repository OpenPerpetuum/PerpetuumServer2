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
