/****** Object:  StoredProcedure [dbo].[changeEIDinOneTable]    Script Date: 10.05.2026 13:22:12 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[changeEIDinOneTable]
   
	@sourceEID bigint, 
	@targetEID BIGINT,
	@tableName VARCHAR(128),
	@columnName VARCHAR(128)
AS
BEGIN
			
	DECLARE @statement NVARCHAR(500), @Parameters NVARCHAR(500)
		
	SET @statement =
    N'IF EXISTS (SELECT * FROM ' + @tableName + ' WHERE ' + @columnName + '=@sourceEID)
    BEGIN
		UPDATE ' + @tableName + ' SET '+@columnName+'=@targetEID WHERE '+@columnName+'=@sourceEID
	END'

	SET @Parameters = N'@sourceEID bigint, @targetEID bigint'
	
	EXEC sp_executesql @statement, @Parameters, @sourceEID, @targetEID
	
END

GO