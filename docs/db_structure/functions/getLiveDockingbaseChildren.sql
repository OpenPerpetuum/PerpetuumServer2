/****** Object:  UserDefinedFunction [dbo].[getLiveDockingbaseChildren]    Script Date: 10.05.2026 10:01:51 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getLiveDockingbaseChildren] () 
RETURNS TABLE 
AS
RETURN 
(
    
WITH baseEids (eid) as
(
SELECT e.eid FROM dbo.entities e
JOIN dbo.zoneentities ze ON e.eid=ze.eid
WHERE e.definition IN (SELECT [definition] FROM dbo.getDockingbaseDefinitions())
UNION
SELECT e.eid FROM dbo.entities e
JOIN dbo.zoneuserentities zue ON e.eid = zue.eid
WHERE e.definition IN (SELECT [definition] FROM dbo.getDockingbaseDefinitions())
)
SELECT * FROM dbo.entities WHERE parent IN (SELECT eid FROM baseEids)
	
)
GO