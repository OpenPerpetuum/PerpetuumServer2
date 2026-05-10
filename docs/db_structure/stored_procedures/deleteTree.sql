/****** Object:  StoredProcedure [dbo].[deleteTree]    Script Date: 10.05.2026 15:04:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[deleteTree] 
(
	@rootEID bigint -- the node which will be used as root
)


--!!!!!! ADMIN TOOL !!!!!!
	
AS
	SET NOCOUNT ON;
	
with children(eid,parent,lvl)
	as
	(
		SELECT eid,parent,0 FROM entities WHERE eid = @rootEID
	
		UNION ALL
	
		SELECT C.eid,C.parent, M.lvl+1	FROM entities AS C JOIN children AS M ON C.parent = M.eid
	)

	delete entities where eid in ( select eid from children   ) option (MAXRECURSION 256)

	 
RETURN







GO