/****** Object:  StoredProcedure [dbo].[getFullTreeByRoot]    Script Date: 10.05.2026 16:04:25 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[getFullTreeByRoot] 
(
	@rootEID bigint -- the node which will be used as root
)

AS
	SET NOCOUNT ON;
	
with children(eid,definition,owner,parent,repackaged,quantity,health,ename,dynprop, lvl)
	as
	(
		SELECT eid,definition,owner,parent,repackaged,quantity,health,ename,dynprop, 0 FROM entities WHERE eid = @rootEID
	
		UNION ALL
	
		SELECT C.eid,C.definition,C.owner,C.parent,C.repackaged,C.quantity,C.health,C.ename,c.dynprop, M.lvl+1	FROM entities AS C JOIN children AS M ON C.parent = M.eid
	)

select eid,definition,owner,parent,repackaged,quantity,health,ename,dynprop,lvl from children option (MAXRECURSION 7) 
	 
RETURN







GO