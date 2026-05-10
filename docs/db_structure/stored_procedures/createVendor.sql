/****** Object:  StoredProcedure [dbo].[createVendor]    Script Date: 10.05.2026 13:41:35 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE PROCEDURE [dbo].[createVendor]
(
	@definition int,
	@marketEID bigint,
	@sellProfit float,
	@buyProfit float
)
AS

BEGIN tran

declare @owner as bigint

select @owner = owner from entities where eid = @marketEID

declare @vendorEID as bigint

declare @baseEID as bigint

select @baseEID = parent from entities where eid = @marketEID

insert into entities (definition, owner, parent) values (@definition,@owner,@baseEID)

select @vendorEID = eid from entities where eid = scope_identity()

if @vendorEID = 0 
begin
	select null as vendorEID
	rollback
	return
end

insert vendors (vendoreid,marketeid,vendorsellprofit,vendorbuyprofit) values (@vendorEID,@marketEID,@sellProfit,@buyProfit)

select @vendorEID as vendorEID

commit











GO