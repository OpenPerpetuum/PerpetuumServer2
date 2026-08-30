/****** Object:  UserDefinedFunction [dbo].[getPBSDefinitionFromCapsule]    Script Date: 10.05.2026 10:42:40 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getPBSDefinitionFromCapsule] 
(
	@capsuleDefinition int
)
RETURNS int
AS
BEGIN
	-- Declare the return variable here
	DECLARE @objectDefinition INT, @pbsDefinition INT

	SELECT @objectDefinition=targetdefinition FROM dbo.definitionconfig WHERE definition=@capsuleDefinition

	IF (@objectDefinition IS NULL)
	BEGIN
		RETURN 0;
	END  

	SELECT @pbsDefinition=targetdefinition FROM dbo.definitionconfig WHERE definition=@objectDefinition
	
	IF (@objectDefinition IS NULL)
	BEGIN
		RETURN 0;
	END  
	
	RETURN @pbsDefinition 
END
GO