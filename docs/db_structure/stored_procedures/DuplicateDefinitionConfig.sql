/****** Object:  StoredProcedure [dbo].[DuplicateDefinitionConfig]    Script Date: 10.05.2026 15:06:33 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[DuplicateDefinitionConfig]
	@sourceDefinition int, 
	@targetDefinition int 
AS
BEGIN
	SET NOCOUNT ON;

	INSERT dbo.definitionconfig
         (
		  definition ,
          targetdefinition ,
          summonerscount ,
          npcpresenceid ,
          item_work_range ,
          explosion_radius ,
          cycle_time ,
          damage_chemical ,
          damage_explosive ,
          damage_kinetic ,
          damage_thermal ,
          lifetime ,
          activationtime ,
          waves ,
          missionrelated ,
          constructionradius ,
          action_delay ,
          deploy_radius ,
          transmitradius ,
          constructionlevelmax ,
          blockingradius ,
          chargeamount ,
          inconnections ,
          outconnections ,
          coretransferred ,
          transferefficiency ,
          productionupgradeamount ,
          productionlevel ,
          coreconsumption ,
          effectid ,
          corecalories ,
          corekickstartthreshold ,
          reinforcecountermax ,
          bandwidthusage ,
          bandwidthcapacity ,
          emitradius ,
          tint ,
          typeexclusiverange ,
          network_node_range 
		  )
		  SELECT 

		  @targetDefinition ,
          targetdefinition ,
          summonerscount ,
          npcpresenceid ,
          item_work_range ,
          explosion_radius ,
          cycle_time ,
          damage_chemical ,
          damage_explosive ,
          damage_kinetic ,
          damage_thermal ,
          lifetime ,
          activationtime ,
          waves ,
          missionrelated ,
          constructionradius ,
          action_delay ,
          deploy_radius ,
          transmitradius ,
          constructionlevelmax ,
          blockingradius ,
          chargeamount ,
          inconnections ,
          outconnections ,
          coretransferred ,
          transferefficiency ,
          productionupgradeamount ,
          productionlevel ,
          coreconsumption ,
          effectid ,
          corecalories ,
          corekickstartthreshold ,
          reinforcecountermax ,
          bandwidthusage ,
          bandwidthcapacity ,
          emitradius ,
          tint ,
          typeexclusiverange ,
          network_node_range 
		  
		  FROM dbo.definitionconfig
		  WHERE definition = @sourceDefinition
END
GO