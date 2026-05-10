/****** Object:  StoredProcedure [dbo].[initServer]    Script Date: 10.05.2026 16:38:30 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE PROCEDURE [dbo].[initServer] 
		/*
		cleans the runtime tables
		*/
AS
 	SET NOCOUNT ON
/*    
DBCC TRACEON(1118,-1)
DBCC TRACEON(2371,-1)
DBCC TRACEON(3213,-1)
DBCC TRACEON(3226,-1)
DBCC TRACEON(3604,-1)
*/
 		
 	--set every account's isloggedin
	update accounts set isloggedin=0
 	
 	--set every character to unselected
 	update characters set inuse=0

	-- cleanup channels
	EXEC dbo.deleteUnusedPublicChannels

	-- clean up channelmembers
	delete from channelmembers where memberid in (select characterid from characters where active = 0)
	
	UPDATE dbo.intrusionsites SET intrusionstarttime=NULL WHERE intrusionstarttime<=DATEADD(MINUTE,10,getdate()) AND intrusionstarttime IS NOT null
	
	
	delete from dbo.zoneuserentities WHERE eid NOT in
	(SELECT eid FROM dbo.entities)


	EXEC dbo.missionCleanUpLog
	
	DELETE dbo.zoneuserentities WHERE eid NOT IN (SELECT eid FROM entities)
    DELETE dbo.pbsconnections WHERE  (sourceeid NOT IN (SELECT eid FROM zoneuserentities)) OR (targeteid NOT IN (SELECT eid FROM zoneuserentities))
    DELETE dbo.marketitems WHERE isSell=1 AND isvendoritem=0 AND itemeid NOT IN (SELECT eid FROM dbo.entities)


	RETURN










GO