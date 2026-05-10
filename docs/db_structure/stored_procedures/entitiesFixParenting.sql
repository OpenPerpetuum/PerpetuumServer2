/****** Object:  StoredProcedure [dbo].[entitiesFixParenting]    Script Date: 10.05.2026 15:08:25 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[entitiesFixParenting] 
	
	
AS
BEGIN

DECLARE @isRun INT
SET @isRun = 1;

EXEC dbo.entitiesReportAndDeleteOrphanedByCf @cfString = 'cf_robots',  @run=@isRun
EXEC dbo.entitiesReportAndDeleteOrphanedByCf @cfString = 'cf_robot_components',  @run=@isRun
EXEC dbo.entitiesReportAndDeleteOrphanedByCf @cfString = 'cf_robot_equipment',  @run=@isRun
EXEC dbo.entitiesReportAndDeleteOrphanedByCf @cfString = 'cf_robot_inventory',  @run=@isRun
EXEC dbo.entitiesReportAndDeleteOrphanedByCf @cfString = 'cf_container',  @run=@isRun
EXEC dbo.entitiesReportAndDeleteOrphanedByCf @cfString = 'cf_ammo',  @run=@isRun
EXEC dbo.entitiesReportAndDeleteOrphanedByCf @cfString = 'cf_material',  @run=@isRun
EXEC dbo.entitiesReportAndDeleteOrphanedByCf @cfString = 'cf_station_services',  @run=@isRun
EXEC dbo.entitiesReportAndDeleteOrphanedByCf @cfString = 'cf_production_items',  @run=@isRun
EXEC dbo.entitiesReportAndDeleteOrphanedByCf @cfString = 'cf_field_accessories',  @run=@isRun
EXEC dbo.entitiesReportAndDeleteOrphanedByCf @cfString = 'cf_documents',  @run=@isRun


END
GO