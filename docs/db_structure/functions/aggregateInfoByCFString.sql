/****** Object:  UserDefinedFunction [dbo].[aggregateInfoByCFString]    Script Date: 10.05.2026 9:47:14 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[aggregateInfoByCFString] 
(
	@cfString VARCHAR(128),
	@marginWorst FLOAT,
	@marginBest FLOAT

)
RETURNS 
@result TABLE 
(
	
	field INT, 
	fieldname NVARCHAR(100),
	increasing VARCHAR(5),
	average FLOAT,
	worstvalue FLOAT,
	bestvalue FLOAT,
	worstmargin FLOAT,
	bestmargin FLOAT,
	serie VARCHAR(max),
	mindef VARCHAR(100),
	maxdef VARCHAR(100),
	defserie VARCHAR(max)
)
AS
BEGIN

DECLARE @definitions IntList;
	INSERT @definitions    ( idval )
	SELECT definition FROM dbo.getDefinitionByCFString(@cfString);


	INSERT @result
	        ( field ,
	          fieldname ,
			  increasing,
			  average ,
	          worstvalue ,
	          bestvalue ,
			  worstmargin,
			  bestmargin,
	          serie,
			  mindef,
			  maxdef,
			  defserie
	        )
			SELECT ar.field,ar.fieldname,ar.increasing,
			ROUND(AVG(ar.value),2),
			dbo.bestWorstValues(@definitions, ar.field,0,0,0),
			dbo.bestWorstValues(@definitions, ar.field,1,0,0),
			dbo.bestWorstValues(@definitions, ar.field,0, @marginWorst, @marginBest),
			dbo.bestWorstValues(@definitions, ar.field,1, @marginWorst, @marginBest),
			dbo.aggregateValueSeries(@definitions, ar.field),
			dbo.extremeDefsByCfField(@definitions, ar.field, 0),
			dbo.extremeDefsByCfField(@definitions, ar.field, 1),
			dbo.defNameSeries(@definitions, ar.field)
			FROM dbo.aggregateRecordsByIds(@definitions) ar
			GROUP BY ar.field,ar.fieldname,ar.increasing
						

	RETURN 
END





GO