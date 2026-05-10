/****** Object:  UserDefinedFunction [dbo].[emailByAccountId]    Script Date: 10.05.2026 10:26:20 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[emailByAccountId]
(
	@accountId int
)
RETURNS VARCHAR(50)
AS
BEGIN
	DECLARE @email VARCHAR(50);
	SELECT @email=email FROM dbo.accounts WHERE accountID=@accountId;
	RETURN @email;
	
END
GO