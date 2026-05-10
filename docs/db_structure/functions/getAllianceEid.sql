/****** Object:  UserDefinedFunction [dbo].[getAllianceEid]    Script Date: 10.05.2026 10:31:56 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


create FUNCTION [dbo].[getAllianceEid] 
(
	@characterId int
)
RETURNS bigint
AS
BEGIN
	
	DECLARE @Result bigint, @corpEid bigint
	SELECT @corpEid = (select corporationeid from corporationmembers where memberid=@characterId)
	SELECT @Result = (select allianceeid from alliancemembers where corporationeid=@corpEid)
	RETURN @Result

END

GO