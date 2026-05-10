/****** Object:  StoredProcedure [dbo].[accountPackageBought]    Script Date: 10.05.2026 7:36:53 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[accountPackageBought] 
	
	@accountId int, 
	@packageId int
AS
BEGIN
	SET NOCOUNT ON;

	-- check package existence
	IF EXISTS (SELECT 1 FROM dbo.accountpremiumpackages WHERE accountid=@accountId AND packageid=@packageId)
	BEGIN
		RETURN;
	END


	--log the purchase
    INSERT dbo.accountpremiumpackages ( accountid, packageid ) VALUES  ( @accountId, @packageId );
	
	--return result
	SELECT 1 AS result;            
	
END
GO