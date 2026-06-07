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

        -- Unbought mats: exclude plasma (3271-3274) and any item that can be manufactured
        -- (production_data.product). Using market_orders_configuration here would incorrectly
        -- capture buyback orders for items just removed from the trade list, causing Step 4
        -- to re-place a buy order for them as if they were raw materials.
        INSERT INTO automarket_unbought_resources (itemdefinition, quantity)
        SELECT mi.itemdefinition, SUM(CAST(mi.quantity AS BIGINT))
        FROM marketitems mi
        INNER JOIN entitydefaults ed ON ed.definition = mi.itemdefinition
        WHERE mi.isAutoOrder = 1 AND mi.isSell = 0
          AND mi.itemdefinition NOT IN (3271, 3272, 3273, 3274)
          AND NOT EXISTS (
              SELECT 1 FROM production_data pd_check
              WHERE pd_check.product = ed.definitionname
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
