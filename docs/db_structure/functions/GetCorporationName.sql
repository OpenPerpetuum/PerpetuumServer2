/****** Object:  UserDefinedFunction [dbo].[GetCorporationName]    Script Date: 10.05.2026 10:35:57 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[GetCorporationName] 
(
	@corporationEID bigint
)
RETURNS VARCHAR(64)
AS
BEGIN
	
	DECLARE @Result VARCHAR(64)

	
	SELECT @Result = (SELECT [NAME] FROM corporations WHERE eid=@corporationEID)

	-- Return the result of the function
	RETURN @Result

END
GO