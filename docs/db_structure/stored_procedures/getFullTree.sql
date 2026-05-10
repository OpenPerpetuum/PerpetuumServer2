/****** Object:  StoredProcedure [dbo].[getFullTree]    Script Date: 10.05.2026 16:03:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO






CREATE PROCEDURE [dbo].[getFullTree] 
(
	@ownerEID bigint,
	@rootEID bigint -- the node which will be used as root
)


--NEM HASZNALT, de meg nagyon jol johet

	
AS
	SET NOCOUNT ON;
	
with children(eid,definition,owner,parent,repackaged,quantity,health,ename, lvl)
	as
	(
		SELECT eid,definition,owner,parent,repackaged,quantity,health,ename, 0 FROM entities WHERE eid = @rootEID
	
		UNION ALL
	
		SELECT C.eid,C.definition,C.owner,C.parent,C.repackaged,C.quantity,C.health,C.ename, M.lvl+1	FROM entities AS C JOIN children AS M ON C.parent = M.eid where C.owner = @ownerEID
	)

	--select eid,definition,parent,repackaged,quantity,health,ename from children where eid<>@rootEID option (MAXRECURSION 256)
select eid from children where eid<>@rootEID 
	 
RETURN







GO