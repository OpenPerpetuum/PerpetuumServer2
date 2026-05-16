/****** Object:  UserDefinedFunction [dbo].[fn_CalculateDynamicPlasmaPrices]    Script Date: 10.05.2026 9:53:13 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



---- Create table function to calculate dynamic prices for a given island type

CREATE     FUNCTION [dbo].[fn_CalculateDynamicPlasmaPrices] (@island_type INT)
RETURNS TABLE
AS
RETURN
(
    WITH parameters AS (
        SELECT 
            MIN_PRICE = 
                CASE @island_type 
                    WHEN 1 THEN 25 
                    WHEN 2 THEN 125 
                    WHEN 3 THEN 200 
                    ELSE 0 
                END,
            MAX_PRICE = 
                CASE @island_type 
                    WHEN 1 THEN 150 
                    WHEN 2 THEN 250 
                    WHEN 3 THEN 275 
                    ELSE 0 
                END
    ),
    weights AS (
        SELECT * FROM (VALUES
            ('def_common_reactor_plasma', 1.0),
            ('def_thelodica_reactor_plasma', 1.0),
            ('def_pelistal_reactor_plasma', 1.0),
            ('def_nuimqol_reactor_plasma', 1.0)
        ) AS w(plasma_type, weight)
    ),
    gathered_cte AS (
        SELECT plasma_type, SUM(quantity) AS gathered
        FROM plasma_gathered
        WHERE gathered_on >= DATEADD(DAY, -7, CAST(GETDATE() AS DATE))
        GROUP BY plasma_type
    ),
    sold_cte AS (
        SELECT plasma_type, SUM(quantity) AS sold
        FROM plasma_sold
        WHERE sold_on >= DATEADD(DAY, -7, CAST(GETDATE() AS DATE))
        GROUP BY plasma_type
    )
    SELECT 
        w.plasma_type,
        g.gathered,
        s.sold,
        w.weight,
        CAST(
            (
                CASE 
                    WHEN ISNULL(g.gathered, 0) = 0 THEN p.MAX_PRICE
                    ELSE
                        p.MIN_PRICE + (p.MAX_PRICE - p.MIN_PRICE) * 
                        (
                            CASE 
                                WHEN CAST(ISNULL(s.sold, 0) AS FLOAT) / NULLIF(g.gathered, 1) > 1 THEN 0
                                ELSE 1.0 - (CAST(ISNULL(s.sold, 0) AS FLOAT) / NULLIF(g.gathered, 1))
                            END
                        )
                END
            ) * w.weight AS DECIMAL(10, 2)
        ) AS dynamic_price
    FROM weights w
    CROSS JOIN parameters p
    LEFT JOIN gathered_cte g ON g.plasma_type = w.plasma_type
    LEFT JOIN sold_cte s ON s.plasma_type = w.plasma_type
);

GO