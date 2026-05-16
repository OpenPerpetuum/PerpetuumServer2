/****** Object:  UserDefinedFunction [dbo].[getMissionCategoryName]    Script Date: 10.05.2026 10:40:56 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getMissionCategoryName]
(
	@categoryValue int
)
RETURNS VARCHAR(64)
AS
BEGIN
	
	DECLARE @Result VARCHAR(64)
	SELECT @Result = (SELECT TOP 1 category FROM dbo.missiontypes WHERE categoryvalue=@categoryValue)
	RETURN @Result

END
GO