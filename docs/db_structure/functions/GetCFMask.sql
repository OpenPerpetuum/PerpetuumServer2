/****** Object:  UserDefinedFunction [dbo].[GetCFMask]    Script Date: 10.05.2026 10:34:13 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[GetCFMask] 
(
	
	@cf bigint
)
RETURNS bigint
AS
BEGIN
	
DECLARE  @mask bigint
SET @mask = 0

WHILE (@cf > 0)
BEGIN
		SET @cf = @cf / 256
		SET @mask = @mask * 256
		SET @mask = @mask | 255
END
RETURN @mask

END
GO