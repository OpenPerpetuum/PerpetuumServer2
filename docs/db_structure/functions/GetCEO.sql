/****** Object:  UserDefinedFunction [dbo].[GetCEO]    Script Date: 10.05.2026 10:33:42 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


--mamlasz, bena, balfasz. corprole az itt hardkodolt, majd valami...


create FUNCTION [dbo].[GetCEO]
(
	@corpEID bigint
)
RETURNS int
AS
BEGIN
	
	DECLARE @Result int
	
	SELECT @Result = (select memberid from corporationmembers where corporationeid=@corpEID and ([role] & 1) = 1)

	RETURN @Result

END

GO