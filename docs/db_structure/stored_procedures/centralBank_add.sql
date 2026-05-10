/****** Object:  StoredProcedure [dbo].[centralBank_add]    Script Date: 10.05.2026 13:19:59 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[centralBank_add] 
	(
	@amount float,
	@transactionType int
	)
	/*
	This stored procedure has to update all the incoming cash flow statistics/calculations
 	*/
	
AS
	SET NOCOUNT ON
	
		
	declare @currentBankCredit bigint

	set @currentBankCredit = (select top(1) bankcredit from gameglobals)
	
	set @currentBankCredit = @currentBankCredit + cast(@amount as bigint)

	--insert log
	insert centralbanktransactions (eventtime,transactiontype,amount,bankcredit) values (getdate(),@transactionType, @amount, @currentBankCredit)
	 
	--increase central bank's money
	update gameglobals set bankcredit = @currentBankCredit

	 
	
	RETURN


GO