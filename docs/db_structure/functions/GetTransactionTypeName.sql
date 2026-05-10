/****** Object:  UserDefinedFunction [dbo].[GetTransactionTypeName]    Script Date: 10.05.2026 10:44:28 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

create FUNCTION [dbo].[GetTransactionTypeName] 
(
	
	@enumvalue int
)
RETURNS VARCHAR(128)
AS
BEGIN
	
	DECLARE @Result VARCHAR(128)
	
	SELECT @Result = (SELECT name FROM dbo.transactiontypes WHERE value=@enumvalue)
	
	RETURN @Result

END
GO