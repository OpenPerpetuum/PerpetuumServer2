/****** Object:  StoredProcedure [dbo].[getModulesFromRobot]    Script Date: 10.05.2026 16:09:27 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[getModulesFromRobot]
(
	@robotEID as bigint,
	@categoryFlags as bigint
)
AS
BEGIN
	with children(eid,definition, parent, lvl)
as
(
	select eid,definition, parent, 0 FROM entities WHERE eid = @robotEID
	
	union ALL
	
	select C.eid,C.definition, C.parent, M.lvl+1 FROM entities AS C JOIN children AS M ON C.parent = M.eid 
)


select children.definition from children left outer join entitydefaults on children.definition = entitydefaults.definition where eid<>@robotEID and (entitydefaults.categoryflags & 255) = 19 option (MAXRECURSION 2)
END

GO