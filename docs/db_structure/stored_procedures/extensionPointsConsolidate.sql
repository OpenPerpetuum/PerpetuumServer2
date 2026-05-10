/****** Object:  StoredProcedure [dbo].[extensionPointsConsolidate]    Script Date: 10.05.2026 15:22:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[extensionPointsConsolidate] 
AS
BEGIN

--colidates the extension points in a transaction

SET NOCOUNT ON

DECLARE @now DATETIME
SET @now = GETDATE()
--WAITFOR DELAY '00:00:01';

-- this will take time anyway
CREATE NONCLUSTERED INDEX [IX_h1]
ON [dbo].[extensionpoints] ([eventtime])
INCLUDE ([accountid])

SET TRANSACTION ISOLATION LEVEL READ COMMITTED
BEGIN TRANSACTION

	--insert sum
	INSERT extensionpoints (accountid,points) 
		SELECT accountid,SUM(points) FROM dbo.extensionpoints GROUP BY accountid

	--delete old records
	DELETE dbo.extensionpoints WHERE eventtime < @now

COMMIT

DROP INDEX [IX_h1] ON  [dbo].[extensionpoints]

END
GO