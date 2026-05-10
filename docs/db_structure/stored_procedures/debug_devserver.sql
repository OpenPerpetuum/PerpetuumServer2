/****** Object:  StoredProcedure [dbo].[debug_devserver]    Script Date: 10.05.2026 13:45:11 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[debug_devserver] 
	
AS
BEGIN
	SET NOCOUNT ON;

exec dbo.debug_disablezones

exec dbo.debug_disablenpcs
exec dbo.debug_enablenpcs
exec dbo.debug_enableallzones

update zones set enabled=1 where [name]='zone_TM'
 


UPDATE zones SET enabled=1 WHERE [name]='zone_terraform_test'
 
UPDATE zones SET enabled=0 WHERE [name]='zone_pvp_arena'
 

update zones set enabled=0 where [name]='zone_mini'
 

update zones set enabled=1 where [name]='zone_training'
 


update zones set enabled=1 where [name]='zoneTestBeta'
 
UPDATE npcpresence SET enabled=0 WHERE id=1562


UPDATE dbo.zones SET enabled=1 WHERE id IN (22, 25, 30, 35, 36, 39 )
 


--disable debug presences
update npcpresence set enabled=0 where [name] in ( 'random_flock_gatherer','debug_presence') 

 
--UPDATE dbo.npcpresence SET enabled=1 WHERE presencetype=1

END

GO