/****** Object:  UserDefinedFunction [dbo].[aggregateValueSeries]    Script Date: 10.05.2026 10:17:03 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[aggregateValueSeries] 
(
	@definitions IntList READONLY,
	@fieldId INT
	
	
)
RETURNS VARCHAR(max)
AS
BEGIN
	

DECLARE @delimiter VARCHAR(5) = ' ';
DECLARE @result VARCHAR(max);
SET @result = '';
DECLARE @cVal FLOAT;

DECLARE @moreIsBetter BIT;
SELECT @moreIsBetter=moreisbetter FROM dbo.aggregatefields WHERE id=@fieldId;

DECLARE @valz CURSOR;

IF (@moreIsBetter = 0)
BEGIN
    -- decreasing: smaller value means better performance

	SET @valz = CURSOR LOCAL READ_ONLY FAST_FORWARD FORWARD_ONLY FOR 
	SELECT ar.value FROM dbo.aggregateRecordsByIds(@definitions) ar WHERE ar.field=@fieldId ORDER BY ar.value DESC;

END
ELSE
BEGIN
	
	-- inscreasing: larger value means better performance
		
	SET @valz = CURSOR LOCAL READ_ONLY FAST_FORWARD FORWARD_ONLY FOR 
	SELECT ar.value FROM dbo.aggregateRecordsByIds(@definitions) ar WHERE ar.field=@fieldId ORDER BY ar.value ASC;


END

OPEN @valz; FETCH NEXT FROM @valz INTO @cVal;
WHILE (@@FETCH_STATUS =0)
BEGIN
    --																	123 spaces
	SET @result = @result + CAST(@cVal AS varchar(30))  + @delimiter + '   ';

FETCH NEXT FROM @valz INTO @cVal; END; CLOSE @valz;DEALLOCATE @valz;


SET @result = SUBSTRING(@result, 0 , LEN(@result) - LEN(@delimiter) +1);


RETURN @result;

END




GO