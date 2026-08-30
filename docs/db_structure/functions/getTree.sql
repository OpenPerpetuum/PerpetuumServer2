/****** Object:  UserDefinedFunction [dbo].[getTree]    Script Date: 10.05.2026 10:05:44 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getTree] 
(	
	@rootEID BIGINT
)
RETURNS TABLE 
AS
RETURN 
(
	
with children(eid,definition,owner,parent,repackaged,quantity,health,ename,dynprop, lvl)
	as
	(
		SELECT eid,definition,owner,parent,repackaged,quantity,health,ename,dynprop, 0 FROM entities WHERE eid = @rootEID
	
		UNION ALL
	
		SELECT C.eid,C.definition,C.owner,C.parent,C.repackaged,C.quantity,C.health,C.ename,c.dynprop, M.lvl+1	FROM entities AS C JOIN children AS M ON C.parent = M.eid
	)

select eid,definition,owner,parent,repackaged,quantity,health,ename,dynprop,lvl from children 


)
GO