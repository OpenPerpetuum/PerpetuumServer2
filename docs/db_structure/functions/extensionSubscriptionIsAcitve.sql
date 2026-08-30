/****** Object:  UserDefinedFunction [dbo].[extensionSubscriptionIsAcitve]    Script Date: 10.05.2026 10:29:02 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[extensionSubscriptionIsAcitve] 
(
	@accountID INT,
	@questionedTime DATETIME
)
RETURNS bit
AS
BEGIN
	IF EXISTS (SELECT * FROM extensionsubscription WHERE accountid=@accountID AND starttime < @questionedTime AND endtime > @questionedTime )
	BEGIN
		RETURN 1;
	END 
	
	RETURN 0

END
GO