/****** Object:  UserDefinedFunction [dbo].[extensionPointsCollected]    Script Date: 10.05.2026 10:28:10 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


create FUNCTION [dbo].[extensionPointsCollected] 
(
	@accountID INT
)
RETURNS int
AS
BEGIN
	
DECLARE @dailySum INT, @penaltySum INT, @resultEp INT;

SELECT @dailySum= SUM(points) FROM extensionpoints WHERE accountid=@accountID
select @penaltySum= sum(points) from extensionpointpenalty where accountid=@accountID

SET @dailySum = COALESCE(@dailySum,0);
SET @penaltySum = COALESCE(@penaltySum,0);

SET @resultEp = @dailySum - @penaltySum ;
RETURN @resultEp;

END
GO