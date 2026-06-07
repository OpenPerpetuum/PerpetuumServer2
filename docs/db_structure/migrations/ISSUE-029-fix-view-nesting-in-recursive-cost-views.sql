-- ISSUE-029: Fix nesting level exceeded in usp_RecalculateInsurancePrices
--
-- Root cause: both v_all_production_costs and v_required_raw_materials contain recursive CTEs
-- whose recursive member JOINs against production_data (a VIEW). SQL Server increments the
-- view nesting counter on each recursive iteration that references an external view. On
-- production data with crafting chains deeper than ~28 levels this exceeds the 32-level limit,
-- causing: "Maximum stored procedure, function, trigger, or view nesting level exceeded (limit 32)".
-- The same chains terminate quickly on sparse local data, so the bug does not reproduce locally.
--
-- Fix: inline production_data logic as a local CTE (prod_data) at the top of each affected view.
-- A CTE reference inside a recursive member does not increment the view nesting counter.
-- Semantics are identical: same filter (purchasable=1, enabled=1, hidden=0), same columns.
--
-- Apply while the server is RUNNING (views are schema-bound read-only objects; no downtime needed).
-- Both views are hot-swapped atomically by CREATE OR ALTER VIEW.

-- 1. Fix v_required_raw_materials first (it is referenced by v_all_production_costs)
CREATE OR ALTER VIEW [dbo].[v_required_raw_materials] AS
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
    SELECT
        rb.product  AS product,
        rb.component AS raw_material,
        SUM(rb.total_amount) AS total_quantity
    FROM RecursiveBreakdown rb
    LEFT JOIN prod_data pd ON rb.component = pd.product
    WHERE pd.product IS NULL
    GROUP BY rb.product, rb.component;
GO

-- 2. Fix v_all_production_costs
CREATE OR ALTER VIEW [dbo].[v_all_production_costs] AS
WITH prod_data AS (
    SELECT
        ed.definitionname  AS product,
        ced.definitionname AS components,
        CAST(c.componentamount AS FLOAT) AS amount
    FROM dbo.components c
    INNER JOIN dbo.entitydefaults ed  ON c.definition          = ed.definition
    INNER JOIN dbo.entitydefaults ced ON c.componentdefinition = ced.definition
    WHERE ed.purchasable = 1 AND ed.enabled = 1 AND ed.hidden = 0
),
all_items AS (
    SELECT product AS item FROM prod_data
    UNION
    SELECT components AS item FROM prod_data
),
recursive_materials AS (
    SELECT
        base.item,
        pd.components AS raw_material,
        CAST(pd.amount * 2.1 AS FLOAT) AS quantity
    FROM all_items base
    JOIN prod_data pd ON pd.product = base.item

    UNION ALL

    SELECT
        rm.item,
        pd.components AS raw_material,
        rm.quantity * pd.amount * 2.1 AS quantity
    FROM recursive_materials rm
    JOIN prod_data pd ON rm.raw_material = pd.product
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
            ac.total_quantity * ISNULL(mp.unit_price, msp.price)
        ) AS production_cost_nic
    FROM aggregated_costs ac
    LEFT JOIN latest_market_prices mp
        ON ac.raw_material COLLATE DATABASE_DEFAULT = mp.resource_name COLLATE DATABASE_DEFAULT
    CROSS JOIN max_scarcity_price msp
    GROUP BY ac.product
),
raw_resources AS (
    SELECT
        base.raw_material AS product,
        ISNULL(mp.unit_price, msp.price) AS production_cost_nic
    FROM (SELECT DISTINCT raw_material FROM v_required_raw_materials) base
    LEFT JOIN latest_market_prices mp
        ON base.raw_material COLLATE DATABASE_DEFAULT = mp.resource_name COLLATE DATABASE_DEFAULT
    CROSS JOIN max_scarcity_price msp
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

-- 3. Verify: run usp_RecalculateInsurancePrices to confirm no nesting error
-- EXEC dbo.usp_RecalculateInsurancePrices;
