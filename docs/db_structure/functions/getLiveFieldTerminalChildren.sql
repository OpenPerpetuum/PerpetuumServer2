/****** Object:  UserDefinedFunction [dbo].[getLiveFieldTerminalChildren]    Script Date: 10.05.2026 10:02:26 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getLiveFieldTerminalChildren] 
(
	@zoneID int
) 

RETURNS TABLE 
AS
RETURN 
(
	WITH ftEids (eid) as
	(
		SELECT e.eid FROM dbo.entities e
		JOIN dbo.zoneentities ze ON e.eid=ze.eid
		WHERE e.definition IN (SELECT definition FROM dbo.getDefinitionByCF(131448)) AND
		@zoneID = ze.zoneID
	)
	SELECT * FROM dbo.entities WHERE parent IN (SELECT eid FROM ftEids)
	
)
GO