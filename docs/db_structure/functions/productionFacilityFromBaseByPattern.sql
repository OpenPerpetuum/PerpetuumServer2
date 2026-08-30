/****** Object:  UserDefinedFunction [dbo].[productionFacilityFromBaseByPattern]    Script Date: 10.05.2026 10:53:56 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[productionFacilityFromBaseByPattern] 
(
	@baseEid BIGINT,
	@pattern VARCHAR(50)
)
RETURNS BIGINT
AS
BEGIN
	
	DECLARE @Result BIGINT
	SELECT @Result =eid FROM dbo.entities e WHERE e.parent=@baseEid AND e.[definition] IN (SELECT [definition] FROM  dbo.productionFacilitiesByPattern(@pattern))
	RETURN @Result

END
GO