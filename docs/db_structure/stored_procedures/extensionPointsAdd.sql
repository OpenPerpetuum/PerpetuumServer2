/****** Object:  StoredProcedure [dbo].[extensionPointsAdd]    Script Date: 10.05.2026 15:20:25 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[extensionPointsAdd] 
	
	@basePoints INT, --normal account
	@bonusPoints INT --paid account
    
AS
BEGIN
	SET NOCOUNT ON;


DECLARE  @nofAffectedAccounts INT , @paying INT, @now DATETIME

SET @now = GETDATE() --real db server time

INSERT dbo.extensionpoints( accountid, points )
	  SELECT accountid,@basePoints FROM accounts

--collect data for log
SET @nofAffectedAccounts = @@ROWCOUNT

--write log
INSERT dbo.extensionpointworklog  ( total,paying ) VALUES  ( @nofAffectedAccounts, @nofAffectedAccounts )

EXEC dbo.artifactReset; -- needs to be moved from here !!! yes, daily task... but

SELECT @now

END
GO