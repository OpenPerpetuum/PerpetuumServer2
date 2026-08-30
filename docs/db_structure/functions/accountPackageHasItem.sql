/****** Object:  UserDefinedFunction [dbo].[accountPackageHasItem]    Script Date: 10.05.2026 10:14:27 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[accountPackageHasItem] 
(
	@accountId INT,
	@itemDefinition INT,
	@itemQuantity INT,
	@packageid INT
)
RETURNS int
AS
BEGIN
	
	DECLARE @hasItem INT, @releaseDate DATETIME, @fixDate DATETIME;
	SET @releaseDate = CAST('2015-12-18 21:00:01.000' AS DATETIME);
	SET @fixDate = CAST('2015-12-28 16:57:22.717' AS DATETIME);

	  DECLARE @xmas2015From DATETIME, @xmas2015To DATETIME;
		SET @xmas2015From = CAST( '2015-12-25 20:06:45.000' AS DATETIME);
		SET @xmas2015To =  CAST( '2015-12-25 20:06:51.500' AS DATETIME);


	-- fixed version, default
	SET @hasItem = (
	SELECT COUNT(*) FROM dbo.accountredeemableitems 
	WHERE 
	accountid=@accountId 
	AND [definition]=@itemDefinition 
	AND quantity=@itemQuantity 
	AND creation>@releaseDate
	AND packageid=@packageId
	);

	IF (@hasItem > 0)
	BEGIN
		RETURN 1;
	END
		
	-- bugged period
	SET @hasItem = (
	SELECT COUNT(*) FROM dbo.accountredeemableitems 
	WHERE 
	accountid=@accountId 
	AND [definition]=@itemDefinition 
	AND quantity=@itemQuantity 
	AND creation>@releaseDate
	AND creation<@fixDate
	AND creation NOT BETWEEN @xmas2015From AND @xmas2015To
	AND packageid IS NULL
		);

	IF (@hasItem > 0)
	BEGIN
		RETURN 1;
	END

	RETURN 0;

END
GO