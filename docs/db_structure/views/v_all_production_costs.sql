/****** Object:  View [dbo].[v_all_production_costs]    Script Date: 28.05.2026 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


---- Use dynamic resource_market_prices; fallback is max-scarcity formula (no raw_material_prices dependency)

CREATE   VIEW [dbo].[v_all_production_costs] AS
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
-- Inline max-scarcity fallback: plasma_anchor x ds_ratio_max x 2.0
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
