/****** Object:  StoredProcedure [dbo].[getList]    Script Date: 10.05.2026 16:06:12 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[getList]
 
	@containerCF BIGINT,
	@rootEID BIGINT,
	@ownerEID BIGINT =NULL,
	@allItems BIT = 0,
	@single BIT
	
AS
BEGIN

SET NOCOUNT ON;

IF (@single = 1)
BEGIN
	SELECT eid,definition,owner,parent,repackaged,quantity,health,ename,dynprop, 0 FROM entities WHERE eid = @rootEID
	RETURN
END


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
		where (C.owner = @ownerEID OR @allitems = 1)
		AND 
		
		--add root to starct recursion      and the parent is not a container = the containers will be listen, but not their content
		(M.eid = @rootEID or M.definition NOT IN  (SELECT definition FROM dbo.entitydefaults WHERE (categoryflags & 0xff)=@containerCF))
	)

	select eid,definition,owner,parent,repackaged,quantity,health,ename,dynprop,lvl from children option (MAXRECURSION 4) 

	 



END



GO