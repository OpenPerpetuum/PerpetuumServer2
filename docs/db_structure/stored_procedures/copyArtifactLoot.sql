/****** Object:  StoredProcedure [dbo].[copyArtifactLoot]    Script Date: 10.05.2026 13:35:56 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


-- would be nice to make it table independent in the future...

CREATE PROCEDURE [dbo].[copyArtifactLoot] 
	
	@from int = 0, 
	@to int = 0
AS
BEGIN
	
	SET NOCOUNT ON;

   INSERT dbo.artifactloot (
	artifacttype,
	definition,
	minquantity,
	maxquantity,
	chance
) 

SELECT @to,definition,minquantity,maxquantity,chance FROM artifactloot WHERE artifacttype = @from

END
GO