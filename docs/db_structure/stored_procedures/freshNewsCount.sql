/****** Object:  StoredProcedure [dbo].[freshNewsCount]    Script Date: 10.05.2026 15:55:03 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[freshNewsCount] 
	
	@characterID int,
	@language int
	
AS
BEGIN
	SET NOCOUNT ON;

	declare @lastLogOut smalldatetime	
	
	if not exists (select characterID from characters where characterid=@characterID)
	begin
		select 0
		return
	end
    
	set @lastLogOut = (select lastLogOut from characters where characterid=@characterID)
	
	if (@lastLogOut is null)
	begin
		select 0
		return
	end
	
	SELECT count(*) from news where ntime > @lastLogOut and [language]=@language
END

GO