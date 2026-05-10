/****** Object:  StoredProcedure [dbo].[deleteUnusedPublicChannels]    Script Date: 10.05.2026 15:05:18 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[deleteUnusedPublicChannels]
	
	
	
AS
BEGIN
	SET NOCOUNT ON;

	
SELECT COUNT(*),'channels before' FROM dbo.channels WHERE [type]=0;

DECLARE @threshold DATETIME;
SET @threshold = DATEADD(MONTH, -3, GETDATE());
SELECT @threshold;

DECLARE @minLogin DATETIME;
DECLARE @channelId INT;
DECLARE channels CURSOR STATIC LOCAL READ_ONLY FORWARD_ONLY FOR 
SELECT id FROM dbo.channels WHERE [type]=0; -- public channels
OPEN channels;
FETCH NEXT FROM channels INTO @channelId;
WHILE (@@FETCH_STATUS = 0)
BEGIN
    
	SELECT @minLogin = MAX(lastUsed) FROM dbo.characters WHERE characterID IN (SELECT memberid FROM dbo.channelmembers WHERE channelid=@channelId)

	IF (@minLogin IS NULL or @minLogin < @threshold)
	BEGIN
	    SELECT * FROM channels WHERE id=@channelId
		DELETE dbo.channelmembers WHERE channelid=@channelId;
		DELETE dbo.channels WHERE id=@channelId;
	END


	FETCH NEXT FROM channels INTO @channelId;
END
CLOSE channels; DEALLOCATE channels;
SELECT COUNT(*),'channels after' FROM dbo.channels  WHERE [type]=0;


END

GO