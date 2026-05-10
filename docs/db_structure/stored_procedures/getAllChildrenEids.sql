/****** Object:  StoredProcedure [dbo].[getAllChildrenEids]    Script Date: 10.05.2026 15:59:59 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[getAllChildrenEids] 
(
	@rootEID bigint -- the node which will be used as root
)

AS
	SET NOCOUNT ON;
	
with children(eid,parent,lvl)
	as
	(
		SELECT eid,parent, 0 FROM entities WHERE eid = @rootEID
	
		UNION ALL
	
		SELECT C.eid,C.parent, M.lvl+1	FROM entities AS C JOIN children AS M ON C.parent = M.eid 
	)
select eid from children where eid<>@rootEID 
	 
RETURN





GO