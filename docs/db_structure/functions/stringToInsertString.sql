/****** Object:  UserDefinedFunction [dbo].[stringToInsertString]    Script Date: 10.05.2026 10:56:57 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


create FUNCTION [dbo].[stringToInsertString] 
(
	@p1 VARCHAR(MAX)
)
RETURNS VARCHAR(MAX)
AS
BEGIN
	DECLARE @Result VARCHAR(MAX)

	IF (@p1 IS NULL)
	BEGIN
		SET @Result = 'NULL'
	END
	ELSE  
	BEGIN
		SET @Result = '''' + @p1 + ''''  
	END  

	SET @Result = RTRIM(LTRIM(@Result))

	RETURN @Result

END
GO