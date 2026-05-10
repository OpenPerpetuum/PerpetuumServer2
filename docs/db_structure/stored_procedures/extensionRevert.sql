/****** Object:  StoredProcedure [dbo].[extensionRevert]    Script Date: 10.05.2026 15:25:41 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[extensionRevert]
	@extensionID INT,
	@fee INT
AS
BEGIN
	

SET NOCOUNT ON
DECLARE @characterID INT, @count int

DECLARE characterz CURSOR LOCAL STATIC FORWARD_ONLY READ_ONLY FOR 
	SELECT distinct characterid FROM accountextensionspent WHERE extensionid=@extensionID

OPEN characterz
FETCH NEXT FROM characterz INTO @characterID

WHILE @@FETCH_STATUS = 0
	BEGIN
		
		update characters set credit=credit+@fee where characterid=@characterID;
		DELETE characterextensions WHERE extensionid=@extensionID;
		DELETE accountextensionspent WHERE extensionid=@extensionID;
		
		SET @count = @count +1

	FETCH NEXT FROM characterz INTO @characterID
	END
	    
CLOSE characterz
DEALLOCATE characterz

SELECT @characterID AS [nofCharacters], 'characters got processed' AS [message], @extensionID AS [extensionId]


END
GO