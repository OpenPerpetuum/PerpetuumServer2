/****** Object:  UserDefinedFunction [dbo].[getLiveGammaMarkets]    Script Date: 10.05.2026 10:03:40 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getLiveGammaMarkets] 
(	
)
RETURNS TABLE 
AS
RETURN 
(
	SELECT * FROM dbo.entities WHERE definition=10 and parent IN
		(SELECT eid FROM dbo.getLiveGammaDockingBases())

)
GO