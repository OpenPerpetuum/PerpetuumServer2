/****** Object:  StoredProcedure [dbo].[getTreeEIDs]    Script Date: 10.05.2026 16:30:07 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE PROCEDURE [dbo].[getTreeEIDs] 
(
	@rootEID bigint -- the node which will be used as root
)
	
AS
	SET NOCOUNT ON;
	
with children(eid)
	as
	(
		SELECT eid FROM entities WHERE eid = @rootEID
	
		UNION ALL
	
		SELECT C.eid FROM entities AS C JOIN children AS M ON C.parent = M.eid 
	)

	select eid from children where eid<>@rootEID 

	 
RETURN


GO