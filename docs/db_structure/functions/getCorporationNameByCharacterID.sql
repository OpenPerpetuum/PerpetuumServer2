/****** Object:  UserDefinedFunction [dbo].[getCorporationNameByCharacterID]    Script Date: 10.05.2026 10:37:07 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getCorporationNameByCharacterID] 
(

	@characterID int
)
RETURNS VARCHAR(128)
AS
BEGIN
	
	DECLARE @corpEid BIGINT, @result VARCHAR(128)
	
	
	SET @corpEid = (SELECT corporationeid FROM dbo.corporationmembers WHERE memberid=@characterID)
		
	SET @result = (SELECT NAME FROM corporations WHERE eid=@corpEid)
	
	RETURN @result
	
	
	END
GO