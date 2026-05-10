/****** Object:  UserDefinedFunction [dbo].[getVendorSellPrice]    Script Date: 10.05.2026 10:45:58 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE FUNCTION [dbo].[getVendorSellPrice] 
(
	@vendorEID bigint,
	@definition int
)
RETURNS int
AS
BEGIN
	
	declare @price bigint
	declare @vendorsellprofit float
	declare @quantity int
	
	set @quantity = (select quantity from entitydefaults where definition=@definition)	
	
	
	
	set @price = dbo.calcPrice(@definition) / @quantity -- get price from itemprices
	set @vendorsellprofit = (select vendorsellprofit from vendors where vendorEID=@vendorEID)

	RETURN @price * @vendorsellprofit   

END

GO