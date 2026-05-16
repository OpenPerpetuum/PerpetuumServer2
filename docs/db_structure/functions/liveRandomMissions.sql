/****** Object:  UserDefinedFunction [dbo].[liveRandomMissions]    Script Date: 10.05.2026 10:08:07 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[liveRandomMissions] 
(	
)
RETURNS TABLE 
AS
RETURN 
(
	SELECT * FROM missions WHERE behaviourtype=2 AND title NOT LIKE '%test%' AND listable=1
)
GO