/****** Object:  StoredProcedure [dbo].[copyComponents]    Script Date: 10.05.2026 13:37:06 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[copyComponents] 
	
	@sourceDefinition int, 
	@targetDefinition int
AS
	
	begin tran

	if not exists (select * from entitydefaults where definition=@sourceDefinition)
	begin
		rollback
		return
	end

	if not exists (select * from entitydefaults where definition=@targetDefinition)
	begin
		rollback
		return
	end
	
	delete components where definition=@targetDefinition 
	insert components select @targetDefinition,componentdefinition,componentamount from components where definition=@sourceDefinition
	--delete components where definition=@targetDefinition and componentamount=1 --delete the license

	select * from components where definition=@targetDefinition
	commit



GO