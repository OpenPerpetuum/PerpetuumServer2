/****** Object:  UserDefinedFunction [dbo].[distance2d]    Script Date: 10.05.2026 10:23:56 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE FUNCTION [dbo].[distance2d] 
(
	
	@ox FLOAT,
	@oy FLOAT,
	@x FLOAT,
	@y FLOAT
)
RETURNS float
AS
BEGIN
	
	DECLARE @dx FLOAT, @dy FLOAT
	
	SET @dx = @ox - @x
	SET @dy = @oy - @y
		
	RETURN SQRT(@dx*@dx + @dy*@dy)

END
GO