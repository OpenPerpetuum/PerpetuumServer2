/****** Object:  StoredProcedure [dbo].[accountPackageList]    Script Date: 10.05.2026 7:40:16 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[accountPackageList] 
	
	@accountId int
	
AS
BEGIN
	SET NOCOUNT ON;
	    
	SELECT * FROM dbo.accountpremiumpackages WHERE accountid=@accountId
	
END
GO