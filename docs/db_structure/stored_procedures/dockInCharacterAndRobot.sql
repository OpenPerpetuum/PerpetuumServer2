/****** Object:  StoredProcedure [dbo].[dockInCharacterAndRobot]    Script Date: 10.05.2026 15:05:57 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[dockInCharacterAndRobot] 
	@characterId INT 
AS
BEGIN
	SET NOCOUNT ON;

	-- dock the character in
DECLARE @baseEid BIGINT, @publicContainer BIGINT, @robotEid BIGINT;
SELECT @baseEid=baseEID, @robotEid=activeChassis FROM characters WHERE characterID=@characterId;

-- check base
IF (dbo.isEntityExists(@baseEid)=0)
BEGIN
	SELECT 'ERROR: base not valid', @baseEid AS baseEid, @characterId AS characterId;
	RETURN
END

-- write character
UPDATE characters SET docked=1,zoneID=NULL, positionX=NULL, positionY=NULL WHERE characterID=@characterId;
SELECT 'OK: character got docked in.', @baseEid AS baseEid, @characterId AS characterId;

-- process robot
IF (@robotEid IS NULL OR dbo.isEntityExists(@robotEid)=0)
BEGIN
	SELECT 'OK: robot eid NULL, or not valid, process finished.', @characterId AS characterId, @robotEid AS robotEid;
	RETURN
END

-- check public container
SET @publicContainer = dbo.GetPublicContainerEidByBaseEid(@baseEid);
IF (@publicContainer IS NULL or dbo.isEntityExists(@publicContainer)=0)
BEGIN
	SELECT 'ERROR: public container not valid.', @publicContainer AS publicContainer;
	RETURN
END

UPDATE dbo.entities SET parent=@publicContainer WHERE eid=@robotEid;
SELECT 'OK: robot got parented to public container.', @publicContainer AS publicContainer, @baseEid AS baseEid, @robotEid AS robotEid, @characterId AS characterId;

END
GO