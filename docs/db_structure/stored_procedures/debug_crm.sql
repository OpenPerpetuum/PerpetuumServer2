/****** Object:  StoredProcedure [dbo].[debug_crm]    Script Date: 10.05.2026 13:44:32 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[debug_crm] 
	
AS
BEGIN
	SET NOCOUNT ON;
	
exec dbo.debug_disablenpcs
exec dbo.debug_disablezones
--EXEC dbo.debug_enablenpcs

--new virginia

--UPDATE dbo.zones SET [enabled]=1 WHERE protected=1

update zones set enabled=1 WHERE [name]='zone_TM'


/*
--attalica
update zones set enabled=1 where [name]='zone_ICS'
*/

/*
--daoden
update zones set enabled=1 where id=2
*/

/*
--tellesis
UPDATE dbo.zones SET enabled=1 WHERE id=6
*/

--shinjalar
/*
update zones set enabled=1 where [name]='zone_ASI_pve'
update plugins set enabled=1 where pluginname='zone_7'
*/

/*
--hershfield
update zones set enabled=1 where [name]='zone_TM_pve'
*/


--update zones set enabled=1 where [name]='zone_terraform_test'


/*
update zones set enabled=1 where [name]='zoneTestBeta'
*/

/*
--UPDATE npcpresence SET enabled=1 WHERE id=1562
*/

/*
update zones set enabled=0 where [name]='zone_training'
*/


/*
update zones set enabled=1 where [name]='zone_ASI_pvp'
*/

/*
update zones set enabled=1 where [name]='zone_ICS_A_real'
*/

/*
update zones set enabled=1 where [name]='zone_tm_g_6'
*/

/*
update zones set enabled=1 where [name]='zone_tm_g_7'
*/

/*
UPDATE zones SET enabled=1 WHERE [name]='zone_tm_g_1'

UPDATE zones SET enabled=1 WHERE [name]='zone_ics_g_1'

UPDATE zones SET enabled=1 WHERE [name]='zone_ics_g_2'
*/

/*
UPDATE zones SET enabled=1 WHERE [name]='zone_ICS_A_real'
*/

--debug npc presence

--UPDATE dbo.npcpresence SET enabled=1 WHERE id=1044

--update npcpresence set enabled=0 where [name]='debug_presence'
--update npcpresence set enabled=0 where id=1172

--UPDATE npcpresence SET enabled=0 WHERE [name]='random_flock_gatherer'

/*
UPDATE dbo.zones SET enabled = 1  where id >=20 
*/

END





GO