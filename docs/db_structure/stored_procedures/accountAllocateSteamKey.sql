/****** Object:  StoredProcedure [dbo].[accountAllocateSteamKey]    Script Date: 10.05.2026 7:32:57 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[accountAllocateSteamKey]
	@accountID INT,
	@steamID varchar(64)
AS
BEGIN
	DECLARE @a int, @keyID int, @steamKey varchar(32)

	SET NOCOUNT ON;

	SET TRANSACTION ISOLATION LEVEL READ COMMITTED
	BEGIN  TRANSACTION

	  IF EXISTS (SELECT id FROM steamkeys WHERE accountid = @accountID OR steamID = @steamID)
	  BEGIN
	    COMMIT
	    RETURN 0
	  END

	  SELECT TOP 1 @keyID = id, @steamKey = steamkey FROM steamkeys WHERE accountid IS NULL

	  UPDATE steamkeys SET accountid = @accountid, assigned = GETDATE(), steamID = @steamID WHERE id = @keyID

	  SELECT @keyID as keyID, @steamKey as steamKey
	COMMIT
    RETURN 1
END
GO