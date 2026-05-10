/****** Object:  StoredProcedure [dbo].[findContainerRoot]    Script Date: 10.05.2026 15:53:14 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[findContainerRoot] 

	@publicContainerDefinition int = 166, 
	@corporateHangarDefinition int = 581,
	@itemEid bigint
AS
BEGIN
	
	
	SET NOCOUNT ON;
	
	declare @parentEid bigint, @parentDefinition int
	set @parentEid = @itemEid
	set @parentDefinition = (select definition from entities where eid=@itemEid)

	while (@parentDefinition != @publicContainerDefinition AND @parentDefinition != @corporateHangarDefinition)
	begin
		set @itemEid = @parentEid
		set @parentEid = (select parent from entities where eid=@itemEid)
		set @parentDefinition = (select definition from entities where eid=@parentEid)
		
		if (@parentEid is null)
		begin
			select 0
			return
		end


	end
		
	select @parentEid
	
END
GO