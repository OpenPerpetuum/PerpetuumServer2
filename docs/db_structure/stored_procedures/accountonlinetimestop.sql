/****** Object:  StoredProcedure [dbo].[accountonlinetimestop]    Script Date: 10.05.2026 7:36:01 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[accountonlinetimestop]
	
	@accountID INT,
	@safeLogOut BIT
	
AS
BEGIN
	SET NOCOUNT ON;
	
	UPDATE dbo.accountonlinetime 
	SET loggedout=GETDATE(),safelogout=@safeLogOut
	WHERE accountid=@accountID AND loggedout IS NULL
		
	
END

GO