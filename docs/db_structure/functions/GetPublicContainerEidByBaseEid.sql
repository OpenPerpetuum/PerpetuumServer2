/****** Object:  UserDefinedFunction [dbo].[GetPublicContainerEidByBaseEid]    Script Date: 10.05.2026 10:43:23 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[GetPublicContainerEidByBaseEid] 
(
	@baseEid bigint
)
RETURNS bigint
AS
BEGIN
	DECLARE @containerEid bigint

	SET @containerEid = (SELECT TOP 1 eid FROM dbo.entities WHERE parent=@baseEid AND definition=166)
		
	RETURN @containerEid

END
GO