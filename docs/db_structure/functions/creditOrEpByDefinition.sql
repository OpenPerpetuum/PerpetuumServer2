/****** Object:  UserDefinedFunction [dbo].[creditOrEpByDefinition]    Script Date: 10.05.2026 10:22:34 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



/*
5477	#credit=n200
5481	#credit=n4800
5482	#credit=n2500

5475	#ep=n40000
5479	#ep=n120000
5480	#ep=n140000

*/

CREATE FUNCTION [dbo].[creditOrEpByDefinition] 
(
	@definition int
)
RETURNS int
AS
BEGIN
	
	DECLARE @result INT;
	SET @result = 0;

	IF (@definition = 5477 )
	BEGIN
		SET @result = 200
	END

	IF (@definition = 5481 )
	BEGIN
		SET @result = 4800
	END

	IF (@definition = 5482 )
	BEGIN
		SET @result = 2500
	END

	IF (@definition = 5475 )
	BEGIN
		SET @result = 40000
	END

	IF (@definition = 5479 )
	BEGIN
		SET @result = 120000
	END

	IF (@definition = 5480 )
	BEGIN
		SET @result = 140000
	END

	RETURN @result;
END
GO