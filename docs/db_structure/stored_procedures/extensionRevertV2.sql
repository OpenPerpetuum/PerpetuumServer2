/****** Object:  StoredProcedure [dbo].[extensionRevertV2]    Script Date: 10.05.2026 15:26:24 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE PROCEDURE [dbo].[extensionRevertV2]
	@accountId INT,
	@extensionToRemoveId INT
AS
BEGIN
SET NOCOUNT ON;
DECLARE @tExtId VARCHAR(20), @tCharId VARCHAR(20), @tNewLev VARCHAR(20), @tCurLev VARCHAR(20);

DECLARE  @characterId INT, @extensionId INT, @extensionLevel INT, @recId INT, @fee INT, @newLevel INT, @currentLevel INT;

DECLARE exSpent CURSOR LOCAL FORWARD_ONLY FAST_FORWARD FOR
SELECT extensionid, extensionlevel, characterid, id FROM dbo.accountextensionspent WHERE  accountid=@accountId AND extensionid=@extensionToRemoveId ORDER BY eventtime;
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

		--pay back fee
		SELECT @fee=price FROM extensions WHERE extensionid=@extensionId;
		UPDATE characters SET credit=credit+@fee WHERE characterID=@characterId;
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
			END
			
		END
			
	end
	
	-- remove the spending record
	DELETE dbo.accountextensionspent WHERE id=@recId;

	FETCH NEXT FROM exSpent INTO @extensionId,@extensionLevel,@characterId,@recId;
END
CLOSE exSpent; DEALLOCATE exSpent;

END

GO