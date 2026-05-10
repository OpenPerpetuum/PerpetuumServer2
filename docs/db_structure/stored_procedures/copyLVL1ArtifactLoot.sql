/****** Object:  StoredProcedure [dbo].[copyLVL1ArtifactLoot]    Script Date: 10.05.2026 13:37:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[copyLVL1ArtifactLoot] 
	
	@from int , 
	@to int 
AS
BEGIN
	SET NOCOUNT ON;


--LEVEL 1
INSERT dbo.artifactloot (
	artifacttype,
	definition,
	minquantity,
	maxquantity,
	chance
) 

SELECT @to,al.definition,al.minquantity,al.maxquantity, CASE WHEN ed.definitionname LIKE '%standard%' THEN 0.015 ELSE 0.05 end 
FROM artifactloot al JOIN entitydefaults ed ON al.definition=ed.definition 
WHERE al.artifacttype=@from
AND (ed.definitionname NOT LIKE '%artifact_a%')
	
END
GO