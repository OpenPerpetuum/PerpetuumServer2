/****** Object:  StoredProcedure [dbo].[addDefinitionToBetaMarkets]    Script Date: 10.05.2026 7:43:47 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[addDefinitionToBetaMarkets] 
	@definition INT,
	@price FLOAT
AS
BEGIN
		
SET NOCOUNT ON;

DECLARE @marketEid BIGINT

DECLARE TableCursor CURSOR FOR SELECT eid FROM dbo.getLiveBetaMarkets()

OPEN TableCursor

FETCH NEXT FROM TableCursor INTO @marketEid
WHILE @@FETCH_STATUS = 0
BEGIN
		INSERT dbo.marketitems
		        ( 
				marketeid ,
				itemdefinition ,
				submittereid,
		        duration ,
		        isSell ,
		        price ,
		        quantity ,
		        isvendoritem 
		         
		        )
		VALUES  ( 
				@marketEid , -- marketeid - bigint
		        @definition , -- itemdefinition - int
		        90 , -- submittereid - bigint
		        0 , -- duration - int
		        1 , -- isSell - bit
		        @price , -- price - float
		        -1 , -- quantity - int
		        1  -- isvendoritem - bit
		         
		        )
		
		
		
		
		FETCH NEXT FROM TableCursor INTO @marketEid
END

CLOSE TableCursor

DEALLOCATE TableCursor
	
END
GO