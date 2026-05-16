/****** Object:  UserDefinedFunction [dbo].[onlineGangMembers]    Script Date: 10.05.2026 10:09:27 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[onlineGangMembers] 
(	
	@gangGuid UNIQUEIDENTIFIER
)
RETURNS TABLE 
AS
RETURN 
(
	
SELECT gm.memberid 
FROM gangmembers gm 
join characters c ON c.characterID = gm.memberid
WHERE 
gm.gangid = @gangGuid
AND
c.inUse=1

)
GO