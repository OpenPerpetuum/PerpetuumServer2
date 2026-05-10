/****** Object:  StoredProcedure [dbo].[insertAveragePrice]    Script Date: 10.05.2026 16:40:10 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[insertAveragePrice]
(
	@marketEID bigint,
	@itemDefinition int,
	@price float,
	@quantity bigint,
	@date datetime
)
AS
BEGIN

declare @perPiecePrice float, @currentLowest float, @currentHighest float

set @perPiecePrice = @price / @quantity


if not exists (select marketeid from marketaverageprices where marketeid = @marketEID and itemdefinition = @itemdefinition and date = @date)
begin

	insert into marketaverageprices (marketeid,itemdefinition,totalprice,quantity,date,dailylowest,dailyhighest) 
							values (@marketEID,@itemDefinition,@price,@quantity,@date,@perPiecePrice,@perPiecePrice)

end else
begin

	select @currentLowest=dailylowest, @currentHighest=dailyhighest 
			from marketaverageprices 
			where marketeid = @marketEID and itemdefinition = @itemdefinition and date = @date

	if (@currentLowest > @perPiecePrice)
	begin
		set @currentLowest = @perPiecePrice
	end

	if (@currentHighest < @perPiecePrice)
	begin
		set @currentHighest = @perPiecePrice
	end

	update marketaverageprices 
			set 
			totalprice = totalprice + @price,
			quantity = quantity + @quantity,
			dailylowest = @currentLowest,
			dailyhighest = @currentHighest
			where marketeid = @marketEID and itemdefinition = @itemdefinition and date = @date
end

END