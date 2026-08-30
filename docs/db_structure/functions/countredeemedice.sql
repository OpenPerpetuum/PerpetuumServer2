/****** Object:  UserDefinedFunction [dbo].[countredeemedice]    Script Date: 10.05.2026 10:21:59 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[countredeemedice] 
(
	
)
RETURNS int
AS
BEGIN
	
	
	RETURN (SELECT SUM(quantity) FROM accountredeemableitems WHERE definition=5202 AND wasredeemed=1)

END
GO