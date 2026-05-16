/****** Object:  UserDefinedFunction [dbo].[getDefinitionByEID]    Script Date: 10.05.2026 10:38:40 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE FUNCTION [dbo].[getDefinitionByEID]
(
	@eid bigint
)
returns int
AS
BEGIN
	declare @result as int

	select @result = definition from entities where eid = @eid

	return @result
END

GO