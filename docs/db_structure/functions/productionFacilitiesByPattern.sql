/****** Object:  UserDefinedFunction [dbo].[productionFacilitiesByPattern]    Script Date: 10.05.2026 10:10:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[productionFacilitiesByPattern]
(
	@pattern VARCHAR(50)
)
RETURNS 
@facilities TABLE 
(
	[definition] int 
	
)
AS
BEGIN
	
SET @pattern = '%' + @pattern + '%';

INSERT @facilities
        ( [definition] )
SELECT d.[definition] FROM dbo.entitydefaults d WHERE d.[definition] IN (SELECT definition FROM dbo.getDefinitionByCFString('cf_production_facilities'))
AND
(
d.definitionname LIKE '%basic%' OR
d.definitionname LIKE '%advanced%' OR
d.definitionname LIKE '%expert%' OR
d.definitionname LIKE '%super%')
AND d.definitionname NOT LIKE '%insurance%'
AND d.definitionname LIKE @pattern
	
	RETURN 
END
GO