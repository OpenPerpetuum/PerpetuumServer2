/****** Object:  UserDefinedFunction [dbo].[missionLocationsFromZone]    Script Date: 10.05.2026 10:08:50 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[missionLocationsFromZone]
(	
	@zoneId int
	
)
RETURNS TABLE 
AS
RETURN 
(
	SELECT * FROM dbo.missionlocations WHERE zoneid=@zoneId
)
GO