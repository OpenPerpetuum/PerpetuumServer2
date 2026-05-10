/****** Object:  UserDefinedFunction [dbo].[CFName]    Script Date: 10.05.2026 10:19:38 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[CFName]
(
	@definition int
)
RETURNS VARCHAR(50)
AS
BEGIN
	
	DECLARE @result VARCHAR(50)
	DECLARE @cfValue BIGINT
	SELECT @cfValue = ed.categoryflags FROM dbo.entitydefaults ed WHERE ed.definition=@definition;
	SELECT @result = cf.name FROM dbo.categoryFlags cf WHERE cf.value = @cfValue; 
	RETURN @Result

END
GO