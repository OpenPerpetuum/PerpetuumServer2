/****** Object:  UserDefinedFunction [dbo].[getBaseEIDByCharacterID]    Script Date: 10.05.2026 10:32:38 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE FUNCTION [dbo].[getBaseEIDByCharacterID]
(
	-- Add the parameters for the function here
	@characterID int
)
RETURNS bigint
AS
BEGIN
	declare @result as bigint

	select @result = (SELECT baseEID from characters where characterid = @characterID)

	RETURN @result

END


GO