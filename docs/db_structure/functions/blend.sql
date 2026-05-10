/****** Object:  UserDefinedFunction [dbo].[blend]    Script Date: 10.05.2026 10:18:22 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE FUNCTION [dbo].[blend] 
(
	@bias FLOAT,
	@valueA FLOAT,
	@valueB FLOAT
)
RETURNS FLOAT
AS
BEGIN
	DECLARE @res FLOAT;

	DECLARE @diff FLOAT;
	SET @diff = @valueB - @valueA;
	SET @res = @valueA + (@diff * @bias);
	
	RETURN @res;

END
GO