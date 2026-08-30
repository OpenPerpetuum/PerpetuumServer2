/****** Object:  UserDefinedFunction [dbo].[getLiveBetaMarkets]    Script Date: 10.05.2026 10:00:19 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


create FUNCTION [dbo].[getLiveBetaMarkets] 
(	
)
RETURNS TABLE 
AS
RETURN 
(
	SELECT eid FROM dbo.entities WHERE definition=10 and parent IN
		(
		SELECT e.eid FROM dbo.zoneentities ze 
		JOIN dbo.entities e ON e.eid=ze.eid
		JOIN dbo.zones z ON ze.zoneID = z.id
		WHERE e.definition IN(SELECT [definition] FROM dbo.getDefinitionByCFString('cf_public_docking_base'))
		AND z.terraformable=0 AND z.protected=0
		)

)
GO