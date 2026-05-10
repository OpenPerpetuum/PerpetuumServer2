/****** Object:  StoredProcedure [dbo].[getItemSummary]    Script Date: 10.05.2026 16:05:40 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[getItemSummary] 
(
	@ownerEID bigint,
	@rootEID bigint -- the node which will be used as root
)
	
AS
	SET NOCOUNT ON;
	
with children(eid,definition,owner,parent,repackaged,quantity,health,ename, lvl)
	as
	(
		SELECT eid,definition,owner,parent,repackaged,quantity,health,ename, 0 FROM entities WHERE eid = @rootEID
	
		UNION ALL
	
		SELECT C.eid,C.definition,C.owner,C.parent,C.repackaged,C.quantity,C.health,C.ename, M.lvl+1	FROM entities AS C JOIN children AS M ON C.parent = M.eid where C.owner = @ownerEID
	)

select  i.definition,i.parent, SUM(CAST(i.quantity AS BIGINT)) from children i  
where i.eid<>@rootEID GROUP BY i.definition,i.parent

	 
RETURN






GO