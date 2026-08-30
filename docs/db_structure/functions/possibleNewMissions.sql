/****** Object:  UserDefinedFunction [dbo].[possibleNewMissions]    Script Date: 10.05.2026 10:10:00 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[possibleNewMissions] 
(	
	
)
RETURNS TABLE 
AS
RETURN 
(
SELECT * FROM missions WHERE missionlevel=-1 AND listable=1
UNION
SELECT * FROM dbo.liveRandomMissions()
UNION
SELECT * FROM missions WHERE missiontype=12
)
GO