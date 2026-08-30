/****** Object:  UserDefinedFunction [dbo].[bestWorstValues]    Script Date: 10.05.2026 10:17:43 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[bestWorstValues] 
(
	@definitions IntList READONLY,
	@fieldId INT,
	@returnBest BIT,
	@marginWorst FLOAT,
	@marginBest FLOAT
)
RETURNS float
AS
BEGIN

DECLARE @moreIsBetter BIT;
SELECT @moreIsBetter=moreisbetter FROM dbo.aggregatefields WHERE id=@fieldId;

DECLARE @worstValue FLOAT, @bestValue FLOAT;

IF (@moreIsBetter=0)
BEGIN
    --decreasing -> smaller the better
	SELECT 
	@worstValue = (1+@marginWorst) * MAX(ar.value), 
	@bestValue = (1-@marginBest) * MIN(ar.value)
	FROM dbo.aggregateRecordsByIds(@definitions) ar WHERE ar.field=@fieldId;

END
ELSE
BEGIN
    --increasing -> larger the better
	SELECT 
	@worstValue = (1-@marginWorst) * MIN(ar.value),
	@bestValue = (1+@marginBest) * MAX(ar.value)
	FROM dbo.aggregateRecordsByIds(@definitions) ar WHERE ar.field=@fieldId;

END

IF (@returnBest=0)
BEGIN
    RETURN @worstValue;
END
	RETURN @bestValue;

END






GO