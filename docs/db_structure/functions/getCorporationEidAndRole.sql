/****** Object:  UserDefinedFunction [dbo].[getCorporationEidAndRole]    Script Date: 10.05.2026 9:54:31 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getCorporationEidAndRole]
(	
	@characterId int
)
RETURNS TABLE 
AS
RETURN 
(
	SELECT corporationEID,[role] FROM dbo.corporationmembers WHERE memberid=@characterId
)
GO