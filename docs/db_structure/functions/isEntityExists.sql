/****** Object:  UserDefinedFunction [dbo].[isEntityExists]    Script Date: 10.05.2026 10:50:59 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


create FUNCTION [dbo].[isEntityExists] 
(
	@eid BIGINT
)
RETURNS bit
AS
BEGIN
	IF EXISTS (SELECT 1 FROM dbo.entities WHERE eid=@eid)
	BEGIN
		RETURN 1;
	END

	RETURN 0;

END
GO