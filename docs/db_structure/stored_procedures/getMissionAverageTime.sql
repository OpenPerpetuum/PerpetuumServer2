/****** Object:  StoredProcedure [dbo].[getMissionAverageTime]    Script Date: 10.05.2026 16:07:43 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[getMissionAverageTime]
	@missionID int
AS
BEGIN
	
	
SET NOCOUNT ON;

DECLARE @missionDoneAmount INT

SET @missionDoneAmount = (SELECT COUNT(*) FROM missionlog WHERE missionID=@missionID AND missionlog.finished IS NOT NULL AND missionlog.succeeded=1)

IF @missionDoneAmount > 50
BEGIN
 
 select 
	AVG(datediff(second,started,finished)) as seconds	
	from missionlog ml where succeeded=1 AND missionID=@missionID 
  
END
	ELSE
BEGIN
 SELECT -1
END



   
	
END
GO