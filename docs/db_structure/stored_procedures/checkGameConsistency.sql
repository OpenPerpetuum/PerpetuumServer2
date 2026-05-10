/****** Object:  StoredProcedure [dbo].[checkGameConsistency]    Script Date: 10.05.2026 13:29:51 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE PROCEDURE [dbo].[checkGameConsistency] 
	
AS
BEGIN
	
	SET NOCOUNT ON;
	
	SELECT 0 FROM accounts;
	

	/*
	declare @disabledEntities int, @disabledMarketItems int, @disabledEnblerExtensions int, @usedInactiveExtensions int
	declare @disabledComponents int

	set @disabledEntities = (select count(*) from entities where definition in (select definition from entitydefaults where enabled=0))
	select 'entities with disabled default found:' + cast(@disabledEntities	as varchar(20))

	set @disabledMarketItems = (select count(*) from marketitems where itemdefinition in (select definition from entitydefaults where enabled=0 or hidden=1))
	select 'marketitems with disabled default found: ' + cast(@disabledMarketItems as varchar(20))
	select 'definition list:'
	select m.itemdefinition,d.definitionname as name from marketitems m join entitydefaults d on m.itemdefinition=d.definition where m.itemdefinition in (select definition from entitydefaults where enabled=0)
	
	set @disabledEnblerExtensions = (select count(*) from enablerextensions where extensionid in (select extensionid from extensions where active=0))
	select 'defaults with disabled enabler extension found: ' + cast(@disabledEnblerExtensions as varchar(20))
	select 'definition list:'
	select e.definition,d.definitionname as name from enablerextensions e join entitydefaults d on e.definition =d.definition where e.extensionid in (select extensionid from extensions where active=0)
	
	set @usedInactiveExtensions = (select count(*) from characterextensions where extensionid in (select extensionid from extensions where active=0))
	select 'character extension found with inactive extension: ' + cast(@usedInactiveExtensions as varchar(20))
	select 'extension list:'
	select c.extensionid,e.extensionname as name from characterextensions c join extensions e on c.extensionid=e.extensionid where c.extensionid in (select extensionid from extensions where active=0)
	
	set @disabledComponents = (select count(distinct definition) from components where componentdefinition in (select definition from entitydefaults where enabled=0) )
	select 'components with disabled definition found:' + cast(@disabledComponents as varchar(20))
	select 'definition with troubled component:'
	select c.definition,d.definitionname as name from components c join entitydefaults d on c.definition=d.definition where c.componentdefinition in (select definition from entitydefaults where enabled=0)
		*/
END



GO