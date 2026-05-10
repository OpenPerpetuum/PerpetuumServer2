/****** Object:  StoredProcedure [dbo].[accountEmailConfirm]    Script Date: 10.05.2026 7:34:40 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[accountEmailConfirm] 
	@accountID int 
	
AS
BEGIN
	SET NOCOUNT ON;

	UPDATE accounts SET emailConfirmed=1 WHERE accountID=@accountID


END
GO