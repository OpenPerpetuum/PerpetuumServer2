/****** Object:  StoredProcedure [dbo].[getTreeNonFiltered]    Script Date: 10.05.2026 16:30:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[getTreeNonFiltered] 
(
	@rootEID bigint -- the node which will be used as root
)



AS
	SET NOCOUNT ON;
	
with children(eid,definition,owner,parent,repackaged,quantity,health,ename, lvl)
	as
	(
		SELECT eid,definition,owner,parent,repackaged,quantity,health,ename, 0 FROM entities WHERE eid = @rootEID
	
		UNION ALL
	
		SELECT C.eid,C.definition,C.owner,C.parent,C.repackaged,C.quantity,C.health,C.ename, M.lvl+1	FROM entities AS C JOIN children AS M ON C.parent = M.eid 
	)

	select eid,owner,definition,parent,repackaged,quantity,health,ename from children where eid<>@rootEID

	 
RETURN







GO