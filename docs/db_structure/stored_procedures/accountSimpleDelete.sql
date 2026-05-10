/****** Object:  StoredProcedure [dbo].[accountSimpleDelete]    Script Date: 10.05.2026 7:42:21 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



--Delete an account. Used from MCP.

CREATE PROCEDURE [dbo].[accountSimpleDelete] 
	
	@accountID int
	
AS
BEGIN transaction
	SET NOCOUNT ON;

	--disconnect character from account
	update characters set accountID=null where accountID=@accountID
		
	--delete from accounts
	delete accounts where accountID=@accountID
   
commit



GO