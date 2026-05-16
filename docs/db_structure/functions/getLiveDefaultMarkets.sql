/****** Object:  UserDefinedFunction [dbo].[getLiveDefaultMarkets]    Script Date: 10.05.2026 10:01:09 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


create FUNCTION [dbo].[getLiveDefaultMarkets] 
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
		WHERE e.definition IN(SELECT [definition] FROM dbo.getDefinitionByCFString('cf_public_docking_base'))
		)

)
GO