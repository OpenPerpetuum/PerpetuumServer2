/****** Object:  UserDefinedFunction [dbo].[GetRandomEid]    Script Date: 10.05.2026 10:43:54 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE FUNCTION [dbo].[GetRandomEid] 
(	
)
RETURNS bigint
AS
BEGIN
	
	DECLARE @Result BIGINT, @bmax BIGINT, @bmin BIGINT , @diff BIGINT, @ftmp FLOAT
	
	SET @bmax = 576460752303423487  
	SET @bmin = 8589934591
	SET @diff = @bmax - @bmin
	
	SET @ftmp = ((SELECT TOP 1 * FROM dbo.randomView) * @diff) + @bmin
	

	SET @Result =  (CAST(@ftmp AS BIGINT))
	


	RETURN @Result

END
GO