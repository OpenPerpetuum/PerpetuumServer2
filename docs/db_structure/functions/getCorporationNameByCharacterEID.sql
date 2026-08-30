/****** Object:  UserDefinedFunction [dbo].[getCorporationNameByCharacterEID]    Script Date: 10.05.2026 10:36:31 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


create FUNCTION [dbo].[getCorporationNameByCharacterEID] 
(

	@characterEID bigint
)
RETURNS VARCHAR(128)
AS
BEGIN
	
	DECLARE @corpEid BIGINT, @result VARCHAR(128), @characterID int
	
	SET @characterID = (SELECT characterID FROM characters WHERE rootEID=@characterEID)
	
	SET @corpEid = (SELECT corporationeid FROM dbo.corporationmembers WHERE memberid=@characterID)
		
	SET @result = (SELECT NAME FROM corporations WHERE eid=@corpEid)
	
	RETURN @result
	
	
	END
GO