/****** Object:  UserDefinedFunction [dbo].[extensionPointsAvailable]    Script Date: 10.05.2026 10:26:59 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[extensionPointsAvailable] 
(
	@accountID INT
)
RETURNS int
AS
BEGIN
	
DECLARE @dailySum INT, @penaltySum INT, @ingameSpent INT, @resultEp INT;

SELECT @dailySum= SUM(points) FROM extensionpoints WHERE accountid=@accountID
select @penaltySum= sum(points) from extensionpointpenalty where accountid=@accountID
SELECT @ingameSpent= SUM(points) FROM accountextensionspent WHERE accountID=@accountID

SET @dailySum = COALESCE(@dailySum,0);
SET @penaltySum = COALESCE(@penaltySum,0);
SET @ingameSpent = COALESCE(@ingameSpent,0);

SET @resultEp = @dailySum - @penaltySum - @ingameSpent;
RETURN @resultEp;

END
GO