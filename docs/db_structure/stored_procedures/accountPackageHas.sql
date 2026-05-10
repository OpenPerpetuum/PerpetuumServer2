/****** Object:  StoredProcedure [dbo].[accountPackageHas]    Script Date: 10.05.2026 7:39:06 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[accountPackageHas] 
	
	@accountId int, 
	@packageId int
AS
BEGIN
	SET NOCOUNT ON;
	    
	SELECT dbo.accountPackageIsPurchased(@accountId,@packageId) AS haspackage;
	
END
GO