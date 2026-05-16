/****** Object:  UserDefinedFunction [dbo].[randomHyp]    Script Date: 10.05.2026 10:54:29 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


create FUNCTION [dbo].[randomHyp] 
(
	@exponentWorst FLOAT,
	@exponentBest FLOAT,
	@biasX FLOAT,
	@biasY FLOAT,
	@worstValue	 FLOAT,
	@bestValue FLOAT
)
RETURNS FLOAT
AS
BEGIN
	
	DECLARE @res FLOAT, @rnd FLOAT
	SET @rnd = (SELECT TOP 1 * FROM dbo.randomView);
	
	--SET @res = dbo.biasedHyp(@rnd,@exponentWorst,@exponentBest,@biasX,@biasY,@worstValue,@bestValue);
		SET @res = 0; -- hotfix	
	RETURN @res;
	
END
GO