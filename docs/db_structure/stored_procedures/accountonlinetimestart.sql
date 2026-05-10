/****** Object:  StoredProcedure [dbo].[accountonlinetimestart]    Script Date: 10.05.2026 7:35:13 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[accountonlinetimestart]
	
	@accountID INT,
	@ip VARCHAR(50),
	@hwHash VARCHAR(50) = NULL,
	@isTrial bit
	
AS
BEGIN
	SET NOCOUNT ON;
	
	IF EXISTS (SELECT accountid FROM dbo.accountonlinetime WHERE accountid=@accountID AND loggedout IS NULL)
	BEGIN
		UPDATE dbo.accountonlinetime 
		SET loggedout = getdate()
		WHERE accountid=@accountID AND loggedout IS NULL
	END
	
	
	
	INSERT dbo.accountonlinetime (accountid,ip,hwhash,istrial) VALUES 
	( 
		@accountID,
		@ip,
		@hwHash,
		@isTrial
	)
		
	
END


GO