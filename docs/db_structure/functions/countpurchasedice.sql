/****** Object:  UserDefinedFunction [dbo].[countpurchasedice]    Script Date: 10.05.2026 10:21:23 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[countpurchasedice] 
(
	
)
RETURNS int
AS
BEGIN
	
	RETURN (SELECT SUM(quantity) FROM accountredeemableitems WHERE definition=5202)

END
GO