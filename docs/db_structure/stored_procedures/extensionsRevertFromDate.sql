/****** Object:  StoredProcedure [dbo].[extensionsRevertFromDate]    Script Date: 10.05.2026 15:28:12 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[extensionsRevertFromDate]
	@accountId INT,
	@fromDate DATETIME
AS
BEGIN
SET NOCOUNT ON;
DECLARE @message VARCHAR(512), @tExtId VARCHAR(20), @tCharId VARCHAR(20), @tNewLev VARCHAR(20), @tCurLev VARCHAR(20), @epDiff INT;

DECLARE  @characterId INT, @extensionId INT, @extensionLevel INT, @recId INT, @fee INT, @preEp INT, @postEp INT, @newLevel INT, @currentLevel INT;
SET @preEp = dbo.extensionPointsAvailable(@accountId);

DECLARE exSpent CURSOR LOCAL FORWARD_ONLY FAST_FORWARD FOR
SELECT extensionid, extensionlevel, characterid, id FROM dbo.accountextensionspent WHERE  accountid=@accountId AND eventtime>@fromDate ORDER BY eventtime;
OPEN exSpent;
FETCH NEXT FROM exSpent INTO @extensionId,@extensionLevel,@characterId,@recId;
WHILE (@@FETCH_STATUS =0)
BEGIN
	SET @tExtId = CAST(@extensionId AS VARCHAR(20));
	SET @tCharId = CAST(@characterId AS VARCHAR(20));
	
	-- degrade the current extension level
	IF (@extensionLevel = 1)
	BEGIN
	    --remove character extension entry
		DELETE dbo.characterextensions WHERE characterid=@characterId AND extensionid=@extensionId
		SET @message = 'character extension deleted. extensionId:' + @tExtId + ' characterId:' + @tCharId ;	RAISERROR( @message,0,1) WITH NOWAIT ;

		--pay back fee
		SELECT @fee=price FROM extensions WHERE extensionid=@extensionId;
		UPDATE characters SET credit=credit+@fee WHERE characterID=@characterId;
		SET @message = 'extension fee returned. fee:' +CAST(@fee AS VARCHAR(20)) + ' characterId:' + @tCharId ;	RAISERROR( @message,0,1) WITH NOWAIT ;
	END
	ELSE
    BEGIN
		
		IF EXISTS (SELECT 1 FROM dbo.characterextensions WHERE characterid=@characterId AND extensionid=@extensionId)
		BEGIN

			SET @newLevel = @extensionLevel-1;
			SET @tNewLev = CAST(@newLevel AS VARCHAR(20));
			SET @currentLevel= (select extensionLevel FROM dbo.characterextensions WHERE characterid=@characterId AND extensionid=@extensionId);
			SET @tCurLev = CAST(@currentLevel AS VARCHAR(20));

			IF (@currentLevel IS NOT NULL AND @currentLevel > @newLevel)
			BEGIN
				UPDATE dbo.characterextensions SET extensionlevel=@newLevel WHERE characterid=@characterId AND extensionid=@extensionId    
				SET @message = 'downgrading ext:' + @tExtId + '  from:'+ @tCurLev + ' => ' + @tNewLev +'  characterId:' + @tCharId ;	RAISERROR( @message,0,1) WITH NOWAIT ;
			END
			ELSE
			BEGIN
				SET @message = 'nothing to do ext:' + @tExtId + '  current:'+ @tCurLev + ' new level:' + @tNewLev +'  characterId:' + @tCharId ; RAISERROR( @message,0,1) WITH NOWAIT ;
			END
		
		END
			
	end
	
	-- remove the spending record
	DELETE dbo.accountextensionspent WHERE id=@recId;
	SET @message = 'one spending entry done.  ext:' + @tExtId + '  characterId:' + @tCharId ;	RAISERROR( @message,0,1) WITH NOWAIT ;

	FETCH NEXT FROM exSpent INTO @extensionId,@extensionLevel,@characterId,@recId;
END
CLOSE exSpent; DEALLOCATE exSpent;
SET @postEp = dbo.extensionPointsAvailable(@accountId);
SET @epDiff = @postEp - @preEp;

SELECT @accountId AS accountId, @preEp AS preEp, @postEp AS postEp, @epDiff AS epDiff
SET @message = 'preEp:' + CAST(@preEp AS VARCHAR(20)) +  ' postEp:' + CAST(@postEp AS VARCHAR(20)) + ' epDiff:' + CAST(@epDiff AS VARCHAR(20))  ; RAISERROR( @message,0,1) WITH NOWAIT ;
SET @message = '-----' ;	RAISERROR( @message,0,1) WITH NOWAIT ;
END
GO