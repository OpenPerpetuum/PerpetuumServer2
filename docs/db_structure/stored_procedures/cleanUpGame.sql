/****** Object:  StoredProcedure [dbo].[cleanUpGame]    Script Date: 10.05.2026 13:32:02 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO





CREATE PROCEDURE [dbo].[cleanUpGame] 
	
AS
BEGIN tran

	--inactive characters lifespan < inactivespan
	select characterID from characters where active=0 and ((datediff(minute, creation,deletedat)) < (datediff(minute, deletedat, getdate())))

	--market

	--disabled items on market	
	--LIST
	select mi.itemdefinition,d.definitionname from marketitems mi join entitydefaults d on mi.itemdefinition=d.definition where mi.itemdefinition in (select definition from entitydefaults where enabled=0)
	--DELETE
	delete marketitems where itemdefinition in (select definition from entitydefaults where enabled=0)

	--items belong to inactive characters
	--LIST
	select mi.itemdefinition,d.definitionname,c.nick from marketitems mi join entitydefaults d on mi.itemdefinition=d.definition join characters c on mi.submittereid=c.rooteid
	where mi.submittereid in (select rooteid from characters where active=0 and ((datediff(minute, creation,deletedat)) < (datediff(minute, deletedat, getdate()))))
	--DELETE
	delete marketitems where submittereid in (select rooteid from characters where active=0 and ((datediff(minute, creation,deletedat)) < (datediff(minute, deletedat, getdate()))))
	


	--character extension table

	--inactive extensions
	--LIST
	select e.extensionname,ce.extensionid from characterextensions ce join extensions e on e.extensionid=ce.extensionid where ce.extensionid in (select extensionid from extensions where active=0)
	--DELETE
	delete characterextensions where extensionid in (select extensionid from extensions where active=0)

	--extensions for inactive characters
	--LIST
	select e.characterextensionid,c.nick as nick from characterextensions e join characters c on e.characterid=c.characterid where e.characterid in 
	(select characterID from characters where active=0 and ((datediff(minute, creation,deletedat)) < (datediff(minute, deletedat, getdate()))))
	--DELETE	
	delete characterextensions where characterid in (select characterID from characters where active=0 and ((datediff(minute, creation,deletedat)) < (datediff(minute, deletedat, getdate()))))	
	
	--character settings
	--LIST
	select s.characterid, c.nick as nick from charactersettings s join characters c on s.characterid=c.characterid where s.characterid in (select characterID from characters where active=0 and ((datediff(minute, creation,deletedat)) < (datediff(minute, deletedat, getdate()))))	
	--DELETE
	delete charactersettings where characterid in (select characterID from characters where active=0 and ((datediff(minute, creation,deletedat)) < (datediff(minute, deletedat, getdate()))))	


	--delete entities where definition in (select definition from entitydefaults where enabled=0)
	--select 'entities deleted: ' + cast(@@ROWCOUNT as varchar)
rollback





GO