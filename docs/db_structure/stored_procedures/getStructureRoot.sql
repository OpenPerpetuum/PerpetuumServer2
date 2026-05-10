/****** Object:  StoredProcedure [dbo].[getStructureRoot]    Script Date: 10.05.2026 16:28:41 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[getStructureRoot] 
	
	@eid bigint
	
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @currentParent BIGINT, @currentEid BIGINT
	SET @currentParent = (SELECT parent FROM entities WHERE eid=@eid)
	
	IF @currentParent is NULL OR @currentParent = 0
	BEGIN
		SELECT 0,(SELECT definition FROM entities WHERE eid=@eid)
		return
	END

   

   WHILE @currentParent IS NOT NULL OR @currentParent > 0
   BEGIN
      SELECT @currentParent=parent,@currentEid=eid FROM entities WHERE eid=@currentParent
   END;

   
   SELECT @currentEid,definition FROM entities WHERE  eid=@currentEid
	
	
END
GO