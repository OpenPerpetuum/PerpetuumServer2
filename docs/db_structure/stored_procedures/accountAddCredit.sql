/****** Object:  StoredProcedure [dbo].[accountAddCredit]    Script Date: 10.05.2026 7:32:12 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[accountAddCredit]
	
	@accountId int, 
	@credit int
AS
BEGIN
	SET NOCOUNT ON;

	UPDATE accounts SET credit=credit+@credit WHERE accountID=@accountId;
	
	
	


END
GO