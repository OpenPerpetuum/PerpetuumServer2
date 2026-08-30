/****** Object:  UserDefinedFunction [dbo].[getNickByEid]    Script Date: 10.05.2026 10:42:07 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

create FUNCTION [dbo].[getNickByEid]
(
	@eid bigint
)
returns varchar(64)
AS
BEGIN
	declare @result as varchar(64)

	select @result = (select nick from characters where rooteid=@eid)

	return @result
END

GO