/****** Object:  StoredProcedure [dbo].[getEntityInfo]    Script Date: 10.05.2026 16:01:26 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[getEntityInfo]
(
	@rootEID bigint
)
AS
BEGIN
with children(eid,definition,owner,parent, lvl)
	as
	(
		SELECT eid,definition,owner,parent, 0 FROM entities WHERE eid = @rootEID
	
		UNION ALL
	
		SELECT C.eid,C.definition,C.owner,C.parent, M.lvl+1	FROM entities AS C JOIN children AS M ON C.parent = M.eid
	)

	select eid,definition,owner,parent from children option (MAXRECURSION 256)
END
GO