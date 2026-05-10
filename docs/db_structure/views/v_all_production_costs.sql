/****** Object:  View [dbo].[v_all_production_costs]    Script Date: 10.05.2026 7:27:10 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


---- Use both based and calculated values

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
computed_costs AS (
    SELECT
        ac.product,
        SUM(
            ac.total_quantity * 
            ISNULL(mp.unit_price, base.price_nic)
        ) AS production_cost_nic
    FROM aggregated_costs ac
    LEFT JOIN latest_market_prices mp 
        ON ac.raw_material COLLATE DATABASE_DEFAULT = mp.resource_name COLLATE DATABASE_DEFAULT
    LEFT JOIN raw_material_prices base 
        ON ac.raw_material COLLATE DATABASE_DEFAULT = base.material_name COLLATE DATABASE_DEFAULT
    GROUP BY ac.product
),
raw_resources AS (
    SELECT 
        rmp.material_name AS product,
        ISNULL(mp.unit_price, rmp.price_nic) AS production_cost_nic
    FROM raw_material_prices rmp
    LEFT JOIN latest_market_prices mp 
        ON rmp.material_name COLLATE DATABASE_DEFAULT = mp.resource_name COLLATE DATABASE_DEFAULT
    WHERE NOT EXISTS (
        SELECT 1 FROM production_data pd WHERE pd.product = rmp.material_name
    )
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