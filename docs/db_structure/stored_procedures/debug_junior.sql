/****** Object:  StoredProcedure [dbo].[debug_junior]    Script Date: 10.05.2026 13:58:06 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[debug_junior] 
	
AS
BEGIN
	SET NOCOUNT ON;
	
--exec dbo.debug_enablenpcs
exec dbo.debug_disablenpcs
exec dbo.debug_disablezones


update zones set enabled=1 where [name]='zoneTestBeta'
 
update zones set enabled=1 where [name]='zone_terraform_test'
 



--debug npc presence
update npcpresence set enabled=1 where name='debug_presence'
--update npcpresence set enabled=1 where name='debug_random'
update npcpresence set enabled=1 where name='junior_roaming_teszt'


END
GO