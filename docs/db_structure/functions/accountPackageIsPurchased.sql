/****** Object:  UserDefinedFunction [dbo].[accountPackageIsPurchased]    Script Date: 10.05.2026 10:15:54 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[accountPackageIsPurchased] 
(
	@accountId INT,
	@packageId INT
)
RETURNS bit
AS
BEGIN
	
	DECLARE @hasPack INT;
    SET @hasPack = (SELECT COUNT(*) FROM dbo.accountpremiumpackages WHERE accountid=@accountId AND packageid=@packageId);

	IF (@hasPack > 0)
	BEGIN
		RETURN 1;
	END

	RETURN 0;            
	

END
GO