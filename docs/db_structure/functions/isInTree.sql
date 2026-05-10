/****** Object:  UserDefinedFunction [dbo].[isInTree]    Script Date: 10.05.2026 10:52:06 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE FUNCTION [dbo].[isInTree] 
(
	@childEID bigint,
	@parentEID bigint,
	@maxDepth int
)
RETURNS int
AS
BEGIN
	
	declare @tmpParent bigint
	
	select @tmpParent = parent from entities where eid=@parentEID
	
	while (@tmpParent is not NULL)
	begin
		
		if (@tmpParent = @childEID or  @maxDepth <= 0)
		begin
			return 1
		end		
			
		select @tmpParent = parent from entities where eid=@tmpParent
		set @maxDepth = @maxDepth - 1

	end

	return 0


END

GO