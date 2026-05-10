/****** Object:  StoredProcedure [dbo].[EpForActivityByType]    Script Date: 10.05.2026 15:10:15 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE PROCEDURE [dbo].[EpForActivityByType] 
	
	@topN int = 10, 
	@daysBack int = 30
AS
BEGIN

SET NOCOUNT ON;

DECLARE @atypes IntList
INSERT @atypes
        ( idval )
		SELECT DISTINCT epforactivitytype FROM dbo.epforactivitylog;

DECLARE @earlier DATETIME, @later DATETIME;
SET @later = GETDATE();
SET @earlier = DATEADD(DAY,-1 *@daysBack, @later);

DECLARE @activityType INT;
DECLARE typez CURSOR LOCAL STATIC FORWARD_ONLY FOR SELECT idval FROM @atypes;
OPEN typez;
FETCH NEXT FROM typez INTO @activityType;
WHILE (@@FETCH_STATUS =0)
BEGIN
    
SELECT @activityType AS [atype], dbo.activityNameByType(@activityType) AS [typename]

SELECT TOP (@topN) * FROM 
(SELECT SUM(rawpoints) AS [sumpts],epforactivitylog.characterid,characters.nick FROM dbo.epforactivitylog
inner join characters ON characters.characterid = epforactivitylog.characterid
WHERE (eventtime BETWEEN @earlier AND @later)
AND epforactivitytype=@activityType
 GROUP BY epforactivitylog.characterid, characters.nick

 ) k

 ORDER BY k.sumpts DESC

	FETCH NEXT FROM typez INTO @activityType;
END
CLOSE typez; DEALLOCATE typez;



SELECT TOP 100 SUM(points) AS [finalpoints],SUM(rawpoints) AS [rawpoints], AVG(boostfactor) AS [avgboost], epforactivitylog.characterid, characters.nick FROM dbo.epforactivitylog
inner join characters ON characters.characterid = epforactivitylog.characterid
WHERE (eventtime BETWEEN @earlier AND @later)
GROUP BY epforactivitylog.characterid, characters.nick
ORDER BY [finalpoints] desc;



END
GO