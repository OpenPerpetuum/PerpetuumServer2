/****** Object:  StoredProcedure [dbo].[getFullRobot]    Script Date: 10.05.2026 16:02:44 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[getFullRobot] 
(
	@robotEID bigint, -- the node which will be used as root
	@cf bigint
)

AS
	SET NOCOUNT ON;

		
	with children(eid,definition,owner,parent,health,ename,quantity,repackaged,dynprop, lvl)
		as
		(
			SELECT Q.eid,Q.definition,Q.owner,Q.parent,Q.health,Q.ename,Q.quantity,Q.repackaged,Q.dynprop, 0 FROM entities Q JOIN dbo.entitydefaults D ON Q.definition = D.definition  AND (D.categoryflags & @cf) != @cf   WHERE parent = @robotEID
		
			UNION ALL
		
			SELECT C.eid,C.definition,C.owner,C.parent,C.health,C.ename,C.quantity,C.repackaged,C.dynprop, M.lvl+1
			FROM entities AS C 
			JOIN children AS M ON C.parent = M.eid 
			
		)

		select eid,definition,owner,parent,health,ename,quantity,repackaged,dynprop from children  
		UNION ALL
		select eid,definition,owner,parent,health,ename,quantity,repackaged,dynprop FROM dbo.entities WHERE eid=@robotEID
		UNION ALL
		select T.eid,T.definition,T.owner,T.parent,T.health,T.ename,T.quantity,T.repackaged,T.dynprop FROM dbo.entities T JOIN dbo.entitydefaults N ON T.definition=N.definition AND (N.categoryflags & @cf) = @cf  WHERE T.parent=@robotEID
		option (MAXRECURSION 2)

RETURN















GO