/****** Object:  UserDefinedFunction [dbo].[sha1FromVarchar]    Script Date: 10.05.2026 10:55:42 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

 
CREATE FUNCTION [dbo].[sha1FromVarchar]
(
	@input VARCHAR(max)
)
RETURNS VARCHAR(100)
AS
BEGIN
	DECLARE @Result VARCHAR(100);
	SELECT @Result = LOWER(CONVERT(VARCHAR(100), HASHBYTES('SHA1', @input) ,2));
	RETURN @Result

END
GO