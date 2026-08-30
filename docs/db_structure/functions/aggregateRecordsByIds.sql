/****** Object:  UserDefinedFunction [dbo].[aggregateRecordsByIds]    Script Date: 10.05.2026 9:50:44 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


-- core - eats intlist
CREATE FUNCTION [dbo].[aggregateRecordsByIds] 
(
	@definitions IntList READONLY
)
RETURNS 
@result TABLE 
(
	[definition] INT,
	[field] INT,
	[value] FLOAT,
	[definitionname] VARCHAR(100),
	[fieldname] nvarchar(100),
	[moreisbetter] BIT,
	[increasing] VARCHAR(5)
)
AS
BEGIN
	INSERT @result
	        ( [definition], [field], [value], [definitionname], [fieldname], moreisbetter, increasing )
		
	SELECT av.[definition],av.[field],av.[value],ed.definitionname,fl.name,fl.moreisbetter,dbo.isAggregateFieldMoreIsBetter(av.field)
	FROM dbo.aggregatevalues av
JOIN dbo.entitydefaults ed ON ed.definition = av.definition	
JOIN dbo.aggregatefields fl ON fl.id = av.field
WHERE av.definition IN (
SELECT idval FROM @definitions  )
AND ed.enabled =1
AND fl.usedinconfig =1
	RETURN 
END


GO