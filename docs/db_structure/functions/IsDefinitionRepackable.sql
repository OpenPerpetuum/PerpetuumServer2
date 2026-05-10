/****** Object:  UserDefinedFunction [dbo].[IsDefinitionRepackable]    Script Date: 10.05.2026 10:49:42 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



-- is repackable?
--  !AlwaysStackable && !NonStackable


CREATE FUNCTION [dbo].[IsDefinitionRepackable] 
(
	@definition int
)
RETURNS BIT
AS
BEGIN
	DECLARE @attributeFlags bigint
	
	SET @attributeFlags = (SELECT attributeflags FROM dbo.entitydefaults WHERE [definition]=@definition)

	IF ((@attributeFlags & 3072) = 0)
	BEGIN
		RETURN 1
	END
	  
	RETURN 0
	

END
GO