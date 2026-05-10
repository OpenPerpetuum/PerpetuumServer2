/****** Object:  StoredProcedure [dbo].[addVendorSellItem]    Script Date: 10.05.2026 7:48:32 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE PROCEDURE [dbo].[addVendorSellItem] 
	
	@vendoreid bigint,
	@definition int,
	@price bigint = 0,
	@amount int = 0

AS
BEGIN
	SET NOCOUNT ON;
		
	declare @vendorsellprofit float, @marketEID bigint, @baseEID bigint, @definitionName varchar(128)
	
	-- check definition 
	if not exists (select definitionname from entitydefaults where definition=@definition)
	begin
		select N'definition not exists' as result
		return
	end

/*
	-- check purchasable
	if (select purchasable from entitydefaults where definition=@definition) = 0
	begin
		select N'definition not purchasable' as result
		return
	end
*/
	


    -- get sell profit
	set @vendorsellprofit = (select vendorsellprofit from vendors where vendorEID=@vendoreid)
	if @vendorsellprofit is NULL
	begin
		select N'vendor not exists' as result
		return
	end
	
	-- get definition name
	set @definitionName = (select definitionname from entitydefaults where definition=@definition)

	if (@price = 0)
	begin
		set @price = dbo.calcPrice(@definition)	-- get price from itemprices
		if (@price = 0)
		begin
			select N'item price not defined.' as result
			return
		end
	end
	
	set @price = @price * @vendorsellprofit   

	if (@amount = 0)
	begin
		set @amount = -1 --force infinite amount
	end
	
	set @baseEID = (select parent from entities where eid=@vendoreid)
	
	-- definition = 10 : market
	set @marketEID = (select eid from entities where parent=@baseEID and definition=10)

	insert marketitems (marketeid,itemdefinition,submittereid,isSell,price,quantity,isvendoritem)
	values
	(@marketEID,@definition,@vendoreid,1,@price,@amount,1)

	SELECT  (N'sell order added. \o/ definition:' + cast(@definition as varchar(20))
			+ N' ' + @definitionName + N' price:' + cast(@price as varchar(20))
			+ N' amount:' + cast(@amount as varchar(20))) as result
END

GO