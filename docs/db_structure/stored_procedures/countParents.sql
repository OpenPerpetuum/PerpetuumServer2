/****** Object:  StoredProcedure [dbo].[countParents]    Script Date: 10.05.2026 13:39:00 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



--counts the parenting depth for the input eid

CREATE PROCEDURE [dbo].[countParents] 
	
	@source bigint 
	
AS
BEGIN
	
	SET NOCOUNT ON;
	declare @counter int, @tmpParent bigint
	
	set @counter =0
	set @tmpParent = (select parent from entities where eid=@source)
	
	while (@tmpParent is not null)
	begin

		set @tmpParent = (select parent from entities where eid=@tmpParent)
		set @counter = @counter + 1

	end
    
	SELECT @counter
END

GO