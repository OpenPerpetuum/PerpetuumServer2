/****** Object:  UserDefinedFunction [dbo].[defNameSeries]    Script Date: 10.05.2026 10:23:14 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[defNameSeries] 
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
DECLARE @cDef INT;

DECLARE @moreIsBetter BIT;
SELECT @moreIsBetter=moreisbetter FROM dbo.aggregatefields WHERE id=@fieldId;

DECLARE @valz CURSOR;

IF (@moreIsBetter = 0)
BEGIN
    -- decreasing: smaller value means better performance

	SET @valz = CURSOR LOCAL READ_ONLY FAST_FORWARD FORWARD_ONLY FOR 
	SELECT ar.definition FROM dbo.aggregateRecordsByIds(@definitions) ar WHERE ar.field=@fieldId ORDER BY ar.value DESC;

END
ELSE
BEGIN
	
	-- inscreasing: larger value means better performance
		
	SET @valz = CURSOR LOCAL READ_ONLY FAST_FORWARD FORWARD_ONLY FOR 
	SELECT ar.definition FROM dbo.aggregateRecordsByIds(@definitions) ar WHERE ar.field=@fieldId ORDER BY ar.value ASC;


END

OPEN @valz; FETCH NEXT FROM @valz INTO @cDef;
WHILE (@@FETCH_STATUS =0)
BEGIN
    --																	  123 spaces
	SET @result = @result + dbo.GetDefinitionName(@cDef)  + @delimiter + '   ';

FETCH NEXT FROM @valz INTO @cDef; END; CLOSE @valz;DEALLOCATE @valz;


SET @result = REPLACE(SUBSTRING(@result, 0 , LEN(@result) - LEN(@delimiter) +1), 'def_','')


RETURN @result;

END





GO