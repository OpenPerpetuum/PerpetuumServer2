/****** Object:  UserDefinedFunction [dbo].[calcPrice]    Script Date: 10.05.2026 10:18:58 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



--Calculates a price for a definition
CREATE FUNCTION [dbo].[calcPrice] 
(
	-- Add the parameters for the function here
	@definition int
)
RETURNS float
AS
BEGIN
	
	DECLARE @PRate FLOAT, @isManualPrice BIT, @hasComponents BIT, @tmpPrice float
	
	SET @PRate = (SELECT profitrate FROM dbo.itemprices WHERE definition=@definition)
	
	IF @PRate IS NULL
	BEGIN
		SET @PRate = 0
	END
		
	if (select count(*) from components where definition=@definition) = 0
		BEGIN
			SET @hasComponents = 0
		END
	ELSE
		BEGIN
			SET @hasComponents = 1
		END
	
	SET @isManualPrice = (SELECT manualprice FROM dbo.itemprices WHERE definition=@definition)
	
	IF @isManualPrice IS NULL
	BEGIN
		SET @isManualPrice = 0
	END
		
	-- if the item has components
	IF (@hasComponents = 1 AND @isManualPrice = 0)
		BEGIN
			set @tmpPrice = 
			(select sum(c.componentamount*p.price) from components as c
			 join itemprices as p on p.definition=c.componentdefinition 
			 where c.definition=@definition 
			 AND c.componentdefinition NOT IN (SELECT definition FROM dbo.entitydefaults WHERE (categoryFlags & 0xffff) = 0x0414 ))
		END
	ELSE
		BEGIN
			SET @tmpPrice = 1
		END
	
	IF (@isManualPrice = 1)
	BEGIN
		SET @PRate = 1 --manualprice not scaled by the profit rate
		SET @tmpPrice = (SELECT price FROM dbo.itemprices WHERE definition=@definition)
		
		IF @tmpPrice IS NULL
		BEGIN
			SET @tmpPrice = 0
		END
		
	END
	
	RETURN @tmpPrice * @PRate	
		

END




GO