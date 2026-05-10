/****** Object:  UserDefinedFunction [dbo].[getDefaultDockingBasePositions]    Script Date: 10.05.2026 9:55:14 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getDefaultDockingBasePositions] 
(	
)
RETURNS TABLE 
AS
RETURN 
(

SELECT ze.eid,ze.zoneID,ze.x,ze.y,ze.z ,z.x AS zonex,z.y AS zoney
FROM dbo.zoneentities ze 
JOIN dbo.entities e ON ze.eid = e.eid 
JOIN dbo.zones z ON z.id = ze.zoneID
WHERE e.definition IN 
(
SELECT definition FROM dbo.getDefinitionByCFString('cf_public_docking_base')
)
AND
z.zonetype !=3


	
)
GO