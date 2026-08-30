/****** Object:  UserDefinedFunction [dbo].[tutorialStructures]    Script Date: 10.05.2026 10:13:30 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


/*
submit_item=		9,
use_switch=			10,
use_itemsupply =	13,
*/

-- from all zones, keep it simple

CREATE FUNCTION [dbo].[tutorialStructures] ()
RETURNS TABLE 
AS
RETURN 
(

SELECT eid FROM dbo.zoneentities ze WHERE ze.eid in
(
	SELECT structureeid FROM dbo.missiontargets WHERE
	structureeid IS NOT NULL and
	targettype IN (9,10,13) AND
	missionid IN 
	(
		SELECT id FROM missions WHERE missionlevel=-1 AND listable=1 --tutorial missions
	)
)
AND --ja meg meg structure is
ze.definition IN (SELECT ze.definition FROM dbo.getDefinitionByCFString('cf_mission_structures'))
)



GO