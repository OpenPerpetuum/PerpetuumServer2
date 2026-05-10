/****** Object:  StoredProcedure [dbo].[backupLog]    Script Date: 10.05.2026 13:19:17 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[backupLog] 

AS
BEGIN
	
DECLARE @filename VARCHAR(512), @file VARCHAR(512), @dayString VARCHAR(64), @currentDb VARCHAR(512);
SET @currentDb = DB_NAME()
SET @dayString = dbo.getdaystring(GETDATE())
SET @filename = @currentDb + '_' + @dayString + '_LOG'
SET @file = @filename + '.bak';

EXEC sys.sp_flush_log
print 'log flushed. ' + @file + ' log backup starts.'

--NOFORMAT: don't format media
--NOINIT+SKIP: append or create the backups
--NAME: logical name
--STATS: progress display at every %
--BUFFERCOUNT: how many buffers
--MAXTRANSFERSIZE: the largest unit of transfer in bytes to be used between SQL Server and the backup media. 
--total memory: buffercount * maxtransfersize.
BACKUP LOG @currentDb
TO  DISK = @file 
WITH NOFORMAT, NOINIT, SKIP,  NAME = N'logbackup',  STATS = 4,  BUFFERCOUNT = 64, MAXTRANSFERSIZE = 4194304 

DBCC SQLPERF (LOGSPACE)
--DBCC LOGINFO;


   
END
GO