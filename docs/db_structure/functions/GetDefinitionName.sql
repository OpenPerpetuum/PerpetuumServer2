/****** Object:  UserDefinedFunction [dbo].[GetDefinitionName]    Script Date: 10.05.2026 10:39:15 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[GetDefinitionName] 
(
	
	@definition int
)
RETURNS VARCHAR(128)
AS
BEGIN
	
	DECLARE @Result VARCHAR(128)

	
	SELECT @Result = (SELECT definitionname FROM dbo.entitydefaults WHERE definition=@definition)

	
	RETURN @Result

END
GO