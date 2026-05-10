/****** Object:  StoredProcedure [dbo].[debug_liveserver]    Script Date: 10.05.2026 13:59:22 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE PROCEDURE [dbo].[debug_liveserver] 
	
AS
BEGIN
	SET NOCOUNT ON;

exec dbo.debug_disablezones

exec dbo.debug_enablenpcs
exec dbo.debug_enableallzones

update zones set enabled=1 where [name]='zone_TM'
 


UPDATE zones SET enabled=0 WHERE [name]='zone_terraform_test'
 
UPDATE zones SET enabled=0 WHERE [name]='zone_pvp_arena'
 



UPDATE zones SET enabled=0 WHERE [name]='zone_mini'
 

--disable debug presences
update npcpresence set enabled=0 where [name] in ( 'random_flock_gatherer','debug_presence') 


--gamma off
UPDATE dbo.zones SET enabled = 0  where id >=20 AND id <= 43
 
  




update zones set enabled=1 where [name]='zone_training'
 


UPDATE dbo.zones SET enabled=1 WHERE id IN (22, 25, 30, 35, 36, 39 )
 

UPDATE zones SET enabled=0 WHERE [name]='zone_gammalab'
 

END

GO