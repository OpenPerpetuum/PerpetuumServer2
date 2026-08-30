/****** Object:  UserDefinedFunction [dbo].[isAggregateFieldMoreIsBetter]    Script Date: 10.05.2026 10:48:32 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[isAggregateFieldMoreIsBetter] 
(
	@fieldId INT
)
RETURNS VARCHAR(5)
AS
BEGIN
	DECLARE @result VARCHAR(5) = 'n/a';

	DECLARE @moreIsBetter BIT;
	SELECT @moreIsBetter=moreisbetter FROM dbo.aggregatefields WHERE id=@fieldId;
		
	IF (@moreIsBetter=1)
	BEGIN
	    SET @result = 'Yes';
	END
	
	IF (@moreIsBetter=0)
	BEGIN
	    SET @result = 'No';
	END

	RETURN @result;

END
GO