/****** Object:  StoredProcedure [dbo].[getcorporationstrength]    Script Date: 10.05.2026 16:00:47 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[getcorporationstrength] 
	
	@corporationEID bigint
	
AS
BEGIN


	SET NOCOUNT ON;


SELECT SUM(points) FROM accountextensionspent WHERE characterid in

(
SELECT characterid FROM characters WHERE active=1 AND characterID IN (SELECT memberid FROM corporationmembers WHERE corporationEID=@corporationEID)
)

END
GO