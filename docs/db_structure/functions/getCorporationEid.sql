/****** Object:  UserDefinedFunction [dbo].[getCorporationEid]    Script Date: 10.05.2026 10:35:24 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getCorporationEid] 
(
	@characterId int
)
RETURNS bigint
AS
BEGIN
	
	DECLARE @Result bigint
	SELECT @Result = (select corporationeid from corporationmembers where memberid=@characterId)
	RETURN @Result

END
GO