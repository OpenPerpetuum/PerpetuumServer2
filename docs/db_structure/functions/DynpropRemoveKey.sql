/****** Object:  UserDefinedFunction [dbo].[DynpropRemoveKey]    Script Date: 10.05.2026 10:25:04 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[DynpropRemoveKey] 
(
	@origDynprop VARCHAR(MAX),
	@keyString VARCHAR(4096)
)
RETURNS VARCHAR(MAX)
AS
BEGIN

DECLARE @firstIndex INT,@secondIndex INT,@result VARCHAR(MAX),@tmpS VARCHAR(4096),@patternS VARCHAR(4096)

SET @patternS = '#' + @keyString

SELECT @firstIndex= CHARINDEX(@patternS,@origDynprop) 

SELECT @secondIndex= CHARINDEX('#',@origDynprop,@firstIndex)

IF (@firstIndex=0)
	BEGIN
	RETURN @origDynprop  
	END  


IF (@secondIndex=0)
	BEGIN
	SET @result = SUBSTRING(@origDynprop,@firstIndex-1,0)
	END
ELSE
	BEGIN
	SET @tmpS = SUBSTRING(@origDynprop,@firstIndex,@secondIndex)
	SET @result = REPLACE(@origDynprop,@tmpS,'')
	END


	RETURN @result

END
GO