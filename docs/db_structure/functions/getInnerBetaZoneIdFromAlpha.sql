/****** Object:  UserDefinedFunction [dbo].[getInnerBetaZoneIdFromAlpha]    Script Date: 10.05.2026 10:39:50 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getInnerBetaZoneIdFromAlpha] 
(
	
	@zoneId int
)
RETURNS int
AS
BEGIN
	
	DECLARE @Result int

	SET @Result =
	CASE 
	 WHEN @zoneId=0 THEN 5 
	 WHEN @zoneId=1 THEN 3
	 WHEN @zoneId=2 THEN 4 	
	end

	-- Return the result of the function
	RETURN @Result

END
GO