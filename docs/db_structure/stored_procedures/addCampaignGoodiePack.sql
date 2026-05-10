/****** Object:  StoredProcedure [dbo].[addCampaignGoodiePack]    Script Date: 10.05.2026 7:42:59 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[addCampaignGoodiePack] 
	
	@accountID int , 
	@campaignToken VARCHAR(128) 
AS
BEGIN
	
	SET NOCOUNT ON;
		
	DECLARE @campaignId INT
		
	SET @campaignId = (SELECT id FROM dbo.campaigns WHERE campaigntoken = @campaignToken)
	
	IF (@campaignId IS NULL OR  @campaignId = 0)
	BEGIN
		 
		SELECT -2, 'campaign was not found'
		RETURN
	END
	
	IF EXISTS (SELECT accountid FROM accountcampaignitems WHERE accountid=@accountID AND campaignid=@campaignId)
	BEGIN
		SELECT -1,'pack already exists'
		RETURN
	END
	
	INSERT dbo.accountcampaignitems (accountid,campaignid) 
	VALUES ( @accountID,@campaignId)
	   
	SELECT 0,'success'
END
GO