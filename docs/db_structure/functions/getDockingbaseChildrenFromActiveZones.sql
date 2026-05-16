/****** Object:  UserDefinedFunction [dbo].[getDockingbaseChildrenFromActiveZones]    Script Date: 10.05.2026 9:57:32 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


Create FUNCTION [dbo].[getDockingbaseChildrenFromActiveZones] () 
RETURNS TABLE 
AS
RETURN 
(
    
WITH baseEids (eid) as
(
SELECT e.eid FROM dbo.entities e
JOIN dbo.zoneentities ze ON e.eid=ze.eid
JOIN dbo.zones z1 ON ze.zoneID=z1.id
WHERE e.definition IN (SELECT [definition] FROM dbo.getDockingbaseDefinitions()) AND z1.enabled=1
UNION
SELECT e.eid FROM dbo.entities e
JOIN dbo.zoneuserentities zue ON e.eid = zue.eid
JOIN dbo.zones z2 ON zue.zoneid=z2.id
WHERE e.definition IN (SELECT [definition] FROM dbo.getDockingbaseDefinitions()) AND z2.enabled=1
)
SELECT * FROM dbo.entities WHERE parent IN (SELECT eid FROM baseEids)
	
)
GO