/****** Object:  UserDefinedFunction [dbo].[tutorialMissionTargetsOnZone]    Script Date: 10.05.2026 10:12:54 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[tutorialMissionTargetsOnZone] 
(	
	@zoneId INT 
)
RETURNS TABLE 
AS
RETURN 
(
	
	SELECT * FROM dbo.missiontargets WHERE missionid  IN
	(
	SELECT id FROM missions WHERE sourceagent in
	(SELECT agentid FROM dbo.missionLocationsFromZone(@zoneId))
	AND missionlevel=-1
	AND listable=1
	)

	EXCEPT

	SELECT * FROM dbo.missiontargets WHERE missionid IN 
	(
	SELECT id FROM missions WHERE sourceagent in
	(SELECT agentid FROM dbo.missionLocationsFromZone(@zoneId))
	AND missionlevel !=-1
	AND listable =1
	)

)
GO