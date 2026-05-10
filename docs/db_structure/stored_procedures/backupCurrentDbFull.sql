/****** Object:  StoredProcedure [dbo].[backupCurrentDbFull]    Script Date: 10.05.2026 13:17:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[backupCurrentDbFull] 
	@note VARCHAR(128) = ''
AS
BEGIN

	
DECLARE @filename VARCHAR(512), @dayString VARCHAR(64), @currentDb VARCHAR(512), @logicalName VARCHAR(512);
SET @currentDb = DB_NAME()
SET @dayString = dbo.getdaystring(GETDATE())
SET @filename = @currentDb +'_'+ @dayString + '_' + @note + '.bak'
SET @logicalName = @currentDb + '__' + @dayString
PRINT @filename

--delayed transactions, flush log
EXEC sys.sp_flush_log

BACKUP DATABASE @currentDb TO
DISK = @filename
WITH
NOFORMAT, NOINIT, SKIP,  STATS = 7,  BUFFERCOUNT = 256, MAXTRANSFERSIZE = 4194304,
NAME = @logicalName

END
GO