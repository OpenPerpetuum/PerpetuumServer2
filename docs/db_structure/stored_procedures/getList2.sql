/****** Object:  StoredProcedure [dbo].[getList2]    Script Date: 10.05.2026 16:07:07 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[getList2]
 
	@rootEID BIGINT,
	@ownerEID BIGINT =NULL
	
AS
BEGIN

SET NOCOUNT ON;

IF (@ownerEID IS NULL) 
BEGIN
	SET @ownerEID=(SELECT owner FROM entities WHERE eid=@rooteid)
END;

with children(eid,definition,owner,parent,repackaged,quantity,health,ename,dynprop, lvl)
	as
	(
		--root
		SELECT eid,definition,owner,parent,repackaged,quantity,health,ename,dynprop, 0 FROM entities WHERE eid = @rootEID
	
		UNION ALL
	
		SELECT C.eid,C.definition,C.owner,C.parent,C.repackaged,C.quantity,C.health,C.ename, C.dynprop, M.lvl+1 
		FROM entities AS C 
		
		--join with previous recursion if the parent is the same
		JOIN children AS M ON C.parent = M.eid
		
		--if owner the character or the corporation
		where (C.owner = @ownerEID)
	
	)

	select eid,definition,owner,parent,repackaged,quantity,health,ename,dynprop,lvl from children option (MAXRECURSION 32) 
--	select eid,definition,owner,parent,repackaged,quantity,health,ename,dynprop,lvl from children




END







GO