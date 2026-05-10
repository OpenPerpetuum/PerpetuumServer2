/****** Object:  StoredProcedure [dbo].[extensionSubscriptionStart]    Script Date: 10.05.2026 15:34:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[extensionSubscriptionStart] 
	
	@accountID INT,
	@startTime DATETIME,
	@endTime DATETIME
	
AS
BEGIN
	
	SET NOCOUNT ON;
	    
	INSERT dbo.extensionsubscription ( accountid, starttime, endtime ) VALUES  ( @accountid,@startTime,@endTime )
END
GO