/****** Object:  UserDefinedFunction [dbo].[getAggregateName]    Script Date: 10.05.2026 10:31:20 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getAggregateName] 
(
	@fieldID int
)
RETURNS VARCHAR(100)
AS
BEGIN
	
	DECLARE @Result VARCHAR(100)

	SELECT  @Result=name FROM dbo.aggregatefields WHERE id=@fieldID
	
	RETURN @Result

END
GO