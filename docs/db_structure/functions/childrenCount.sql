/****** Object:  UserDefinedFunction [dbo].[childrenCount]    Script Date: 10.05.2026 10:20:18 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[childrenCount] 
(
	-- Add the parameters for the function here
	@eid bigint
)
RETURNS int
AS
BEGIN
	
	DECLARE @Result int

	
	set @result = (SELECT count(*) from entities where parent=@eid)

	-- Return the result of the function
	RETURN @Result

END
GO