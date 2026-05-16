/****** Object:  UserDefinedFunction [dbo].[getEntitiesFromZoneByCf]    Script Date: 10.05.2026 9:58:56 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getEntitiesFromZoneByCf] 
(	
	@zoneId INT, 
	@cfString VARCHAR(128)
)
RETURNS TABLE 
AS
RETURN 
(
	
WITH columndefs (definition) AS 
(
SELECT [definition] FROM dbo.getDefinitionByCFString(@cfString)
)
, livezoneeids (eid) AS
(
SELECT eid FROM dbo.zoneentities WHERE runtime=0 AND zoneID=@zoneId AND [enabled]=1
)
SELECT * FROM dbo.entities e WHERE e.eid IN (SELECT eid FROM livezoneeids) AND e.definition IN (SELECT definition FROM columndefs)
)

GO