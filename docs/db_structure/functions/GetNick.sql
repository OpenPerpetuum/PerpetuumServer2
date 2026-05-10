/****** Object:  UserDefinedFunction [dbo].[GetNick]    Script Date: 10.05.2026 10:41:32 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[GetNick] 
(

	@characterId int
)
RETURNS VARCHAR(128)
AS
BEGIN
	
	DECLARE @Result VARCHAR(128)

	
	SELECT @Result = (SELECT nick FROM characters WHERE characterid=@characterId)

	
	RETURN @Result

END
GO