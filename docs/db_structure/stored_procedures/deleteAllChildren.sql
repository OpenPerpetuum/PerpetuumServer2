/****** Object:  StoredProcedure [dbo].[deleteAllChildren]    Script Date: 10.05.2026 14:00:26 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[deleteAllChildren] 
(
	@rootEID bigint -- the node which will be used as root
)


AS
	SET NOCOUNT ON;
	
with children(eid,parent,lvl)
	as
	(
		SELECT @rootEid,@rootEid,0 
	
		UNION ALL
	
		SELECT C.eid,C.parent, M.lvl+1	FROM entities AS C JOIN children AS M ON C.parent = M.eid
	)

	delete entities where eid in ( select eid from children ) AND eid<>@rootEID option (MAXRECURSION 256)

SELECT @@ROWCOUNT
	 
RETURN
GO