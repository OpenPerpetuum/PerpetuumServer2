/****** Object:  StoredProcedure [dbo].[centralBank_sub]    Script Date: 10.05.2026 13:21:30 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[centralBank_sub] 
	(
	@amount float,
	@transactionType int
	)
	/* deal with statistics */
	
AS
	SET NOCOUNT ON
	
	declare @currentBankCredit bigint
	set @currentBankCredit = (select top(1) bankcredit from gameglobals)
		
	set @currentBankCredit = @currentBankCredit - cast(@amount as bigint)


	if @currentBankCredit < 0
	begin
		-- trigger function here that the central bank gave loan to itself => 1B
		set @currentBankCredit = @currentBankCredit + 1000000000
	end
	
	--insert log
	insert centralbanktransactions (eventtime,transactiontype,amount,bankcredit) values (getdate(),@transactionType,-1 * @amount, @currentBankCredit)
	 
	--increase central bank's money
	update gameglobals set bankcredit = @currentBankCredit

	RETURN




GO