/****** Object:  UserDefinedFunction [dbo].[getVendorMarketPrice]    Script Date: 10.05.2026 10:45:18 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getVendorMarketPrice] 
(
	@vendorEID bigint,
	@definition INT,
	@isSell BIT
	
)
RETURNS int
AS
BEGIN
	
	DECLARE @manualPrice bit
	declare @price bigint
	declare @vendorProfit float
	declare @quantity int
	
	SET @manualPrice = (SELECT manualprice FROM itemprices WHERE definition =@definition)
	set @quantity = (select quantity from entitydefaults where definition=@definition)	
		
	set @price = dbo.calcPrice(@definition) / @quantity -- get price from itemprices
	
	IF (@isSell=1)
	begin
		set @vendorProfit = (select vendorsellprofit from vendors where vendorEID=@vendorEID)
	END
	ELSE
	BEGIN
		SET @vendorProfit = (SELECT vendorbuyprofit FROM dbo.vendors where vendorEID=@vendorEID)
	END
	
	IF (@manualPrice=1)
	BEGIN
		SET @vendorProfit = 1
	end
	
	
	RETURN @price * @vendorProfit   

END
GO