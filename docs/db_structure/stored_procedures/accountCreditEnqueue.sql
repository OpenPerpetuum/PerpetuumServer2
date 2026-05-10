/****** Object:  StoredProcedure [dbo].[accountCreditEnqueue]    Script Date: 10.05.2026 7:33:47 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[accountCreditEnqueue] 
	@accountId int, 
	@credit int 
AS
BEGIN
	SET NOCOUNT ON;
	SET TRANSACTION ISOLATION LEVEL READ COMMITTED
	BEGIN  TRANSACTION
	INSERT dbo.accountcreditqueue ( accountid, credit ) VALUES ( @accountId, @credit )
	COMMIT
    
END
GO