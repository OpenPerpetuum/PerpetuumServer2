/****** Object:  View [dbo].[production_data]    Script Date: 10.05.2026 7:24:41 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE   VIEW [dbo].[production_data] AS
SELECT
    ed.definition AS itemdefinition,
    ed.definitionname AS product,
    ced.definitionname AS components,
    c.componentamount AS amount
FROM components c
INNER JOIN entitydefaults ed ON c.definition = ed.definition
INNER JOIN entitydefaults ced ON c.componentdefinition = ced.definition
WHERE ed.purchasable = 1 AND ed.enabled = 1 AND ed.hidden = 0;-- AND (ed.tiertype IS NULL OR ed.tiertype = 1);-- AND ed.attributeflags & CONVERT(BIGINT, 2147483648) = 0;

GO