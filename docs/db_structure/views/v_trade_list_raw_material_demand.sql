/****** Object:  View [dbo].[v_trade_list_raw_material_demand]    Script Date: 10.06.2026 7:26:34 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


-- Returns the raw materials (and quantities) required to fulfil the AutoMarket trade list.
-- Used exclusively as a demand signal in recalculate_raw_material_prices.
-- Material ENUMERATION is now driven by entitydefaults (cf_raw_material flag) — not this view.
-- prod_data inlines production_data to avoid view-nesting-level accumulation inside the recursive member.
-- SQL Server increments the view nesting counter on every recursive iteration that references an external
-- view; a CTE reference does not count, so chains deeper than ~28 levels no longer hit the 32-level limit.
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
            SUM(CAST(ROUND(pd.amount * 2.1, 0) AS BIGINT)) AS total_amount  -- 50% efficiency adjustment
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
