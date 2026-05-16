/****** Object:  UserDefinedFunction [dbo].[isInstance]    Script Date: 10.05.2026 10:51:33 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[isInstance]
(
	@zoneID int
)
RETURNS int
AS
BEGIN
	DECLARE @Result int

	SET @Result = (SELECT isInstance FROM dbo.zones WHERE id=@zoneID)
		
	RETURN @Result

END
GO