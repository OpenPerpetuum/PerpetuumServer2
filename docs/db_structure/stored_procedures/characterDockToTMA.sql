/****** Object:  StoredProcedure [dbo].[characterDockToTMA]    Script Date: 10.05.2026 13:26:59 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[characterDockToTMA]
	@characterId int  
	
AS
BEGIN
		SET NOCOUNT ON;
		DECLARE @baseEid BIGINT,@publicContainer BIGINT, @activeRobot BIGINT
		SET @baseEid = 561
		SET @publicContainer = 680
		
		--dock character to tma
		UPDATE characters SET docked=1,zoneID=NULL,positionX=NULL,positionY=NULL,baseEID=@baseEid WHERE characterID=@characterId
		 
		SET @activeRobot = (SELECT activechassis FROM dbo.characters WHERE characterID=@characterId)
		
		IF (@activeRobot IS NOT NULL)
		BEGIN
			--parent robot to tma public container
			UPDATE dbo.entities SET parent=@publicContainer WHERE eid=@activeRobot
		END     
	
END
GO