/****** Object:  StoredProcedure [dbo].[accountPurchase]    Script Date: 10.05.2026 7:41:37 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO




CREATE PROCEDURE [dbo].[accountPurchase] 
	@accountID int
	
	
AS
BEGIN tran
	
	SET NOCOUNT ON;
    
	DECLARE @alreadyActive BIT
  
	SET @alreadyActive = (SELECT isactive FROM accounts WHERE accountID=@accountID)
	
	IF (@alreadyActive = 0)
	BEGIN
		--First purchase activates account and adds starting EP
    
	    UPDATE accounts SET isactive=1 WHERE accountID=@accountID
		
		--works from packages 
		--EXEC dbo.extensionPointsInject  @accountID, 40160 

		-- add standard package
		EXEC dbo.accountPackageBought @accountID, 1
		

	END  
	
	

commit

	--send ok...
	select 1
	return 1







GO