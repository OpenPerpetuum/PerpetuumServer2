/****** Object:  UserDefinedFunction [dbo].[robotTemplateContainsEw]    Script Date: 10.05.2026 10:55:05 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[robotTemplateContainsEw] 
(
	@robotTemplateId int
)
RETURNS int
AS
BEGIN
	
	DECLARE @Result INT, @templateText VARCHAR(MAX)
	SET @Result = 0
	SET @templateText = (SELECT [description] FROM dbo.robottemplates WHERE id=@robotTemplateId)
	

	DECLARE @hexNumber VARCHAR(50), @compareThis VARCHAR(50)

	DECLARE TableCursor CURSOR FOR SELECT hexn FROM dbo.ewModulesInHex()

	OPEN TableCursor

	FETCH NEXT FROM TableCursor INTO @hexNumber
	WHILE @@FETCH_STATUS = 0
	BEGIN
			SET @compareThis = '%i' + @hexNumber + '|%'

			
			IF (@templateText LIKE @compareThis)
			BEGIN
				SET @Result = 1;
			end
            
			SET @compareThis = '%i' + @hexNumber 
			IF (@templateText LIKE @compareThis)
			BEGIN
				SET @Result = 1;
			end

		
		FETCH NEXT FROM TableCursor INTO @hexNumber
	END

	CLOSE TableCursor

	DEALLOCATE TableCursor




	-- Return the result of the function
	RETURN @Result

END
GO