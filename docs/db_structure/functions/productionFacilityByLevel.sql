/****** Object:  UserDefinedFunction [dbo].[productionFacilityByLevel]    Script Date: 10.05.2026 10:53:15 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[productionFacilityByLevel] 
(
	@pattern VARCHAR(50),
	@level VARCHAR(50)
)
RETURNS int
AS
BEGIN
	
	DECLARE @Result int

	SET @pattern = '%' + @pattern + '%';

	SELECT @Result= [definition] FROM dbo.facilitymap WHERE leveltag=@level AND defname LIKE @pattern
	
	RETURN @Result

END
GO