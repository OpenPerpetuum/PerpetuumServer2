/****** Object:  UserDefinedFunction [dbo].[treeEids]    Script Date: 10.05.2026 10:12:16 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


--except root

CREATE FUNCTION [dbo].[treeEids]
(	
	@rootEID bigint 
)
RETURNS TABLE 
AS
RETURN 
(
	
with children(eid)
	as
	(
		SELECT eid FROM entities WHERE eid = @rootEID
	
		UNION ALL
	
		SELECT C.eid FROM entities AS C JOIN children AS M ON C.parent = M.eid 
	)

select eid from children where eid<>@rootEID 

)
GO