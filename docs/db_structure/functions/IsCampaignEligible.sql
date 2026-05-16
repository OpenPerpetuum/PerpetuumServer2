/****** Object:  UserDefinedFunction [dbo].[IsCampaignEligible]    Script Date: 10.05.2026 10:49:07 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[IsCampaignEligible] 
(
	@accountID int, 
	@campaignToken VARCHAR(128)
)
RETURNS int
AS
BEGIN
	DECLARE @campaignID INT
	
	SET @campaignID = (SELECT id FROM dbo.campaigns WHERE campaigntoken=@campaignToken)

	IF EXISTS (SELECT accountid FROM dbo.accountcampaignitems WHERE accountid=@accountID AND campaignid=@campaignID)
	BEGIN
		RETURN 0
	END
    ELSE
    BEGIN
		RETURN 1
	END

	RETURN 0
END
GO