/****** Object:  StoredProcedure [dbo].[centralBank_addLog]    Script Date: 10.05.2026 13:20:45 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[centralBank_addLog] 
	@day smalldatetime

AS
BEGIN
	
	SET NOCOUNT ON

	declare @credit bigint
	set @credit = (select top(1) bankcredit from gameglobals)

	if not exists (select  amount from centralbanklog where eventday=@day)
	begin
		insert centralbanklog ([eventday],amount) values (@day,@credit)
	end
	else
	begin
		update centralbanklog set amount=@credit where eventday=@day
	end

	
END
GO