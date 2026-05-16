/****** Object:  UserDefinedFunction [dbo].[getBetaArtifactIdFromAlphaId]    Script Date: 10.05.2026 10:33:08 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getBetaArtifactIdFromAlphaId] 
(
	
	@artifactType int
)
RETURNS int
AS
BEGIN
	
	DECLARE @Result INT, @newName VARCHAR(50), @currentName VARCHAR(50)
	
	SET @currentName = (SELECT [name] FROM artifacttypes WHERE id=@artifactType)
	
	IF @currentName IS NULL
	BEGIN
	 RETURN -2
	end
		
	
	SET @newName = REPLACE(@currentName,'_alpha','_beta')
	
	SET @Result = (SELECT id FROM artifacttypes WHERE [name]=@newName)
	
	IF @Result IS NULL
	BEGIN
	 RETURN -1
	END
		
	
	-- Return the result of the function
	RETURN @Result

END
GO