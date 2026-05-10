/****** Object:  StoredProcedure [dbo].[epForActivityLogList]    Script Date: 10.05.2026 15:11:18 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[epForActivityLogList] 
	
	@accountId int, 
	@earlier DATETIME,
	@later DATETIME
AS
BEGIN
	SET NOCOUNT ON;



SELECT k.characterid, k.yearpart,k.monthpart,k.daypart, k.epforactivitytype, SUM(points) AS [points]  from
(
SELECT *,DATEPART(YEAR,eventtime) AS [yearpart],DATEPART(MONTH, eventtime) AS [monthpart],DATEPART(DAY,eventtime) AS [daypart] FROM dbo.epforactivitylog WHERE accountid=@accountId AND (eventtime BETWEEN @earlier AND @later)
)  k
GROUP BY k.yearpart, k.monthpart, k.daypart, k.epforactivitytype,k.characterid
ORDER BY k.yearpart, k.monthpart, k.daypart, k.epforactivitytype asc

END
GO