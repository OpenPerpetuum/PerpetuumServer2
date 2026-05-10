/****** Object:  StoredProcedure [dbo].[accountPackageProcessOne]    Script Date: 10.05.2026 7:40:53 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[accountPackageProcessOne] 
	@accountId int, 
	@packageId int
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @iDefName VARCHAR(512), @packageName VARCHAR(512);
	DECLARE @iDef INT, @iQty INT;

	SET @packageName = (SELECT name FROM dbo.packages WHERE id=@packageId);

	DECLARE packItems CURSOR LOCAL FORWARD_ONLY READ_ONLY FOR SELECT [definition],[quantity] FROM dbo.packageitems WHERE packageid = @packageId;
	OPEN packItems;
	FETCH NEXT FROM packItems INTO @iDef, @iQty;
	WHILE(@@FETCH_STATUS =0)
	BEGIN
		
		SET @iDefName = dbo.GetDefinitionName(@iDef);
		--PRINT ( @iDefName + ' ' + CAST(@iQty AS VARCHAR(20)));

		IF (dbo.isDefinitionSparkUnlocker(@iDef)=0)
		BEGIN
			-- not a spark unlocker		
			IF (dbo.accountPackageHasItem(@accountId,@iDef,@iQty, @packageId)=0)
			BEGIN
			
				--has no item yet
				INSERT dbo.accountredeemableitems
						( accountid , [definition] , quantity, packageid )
				VALUES  ( @accountId,  @iDef,         @iQty,   @packageId )
				
				PRINT ( 'accountId:' + CAST(@accountId AS VARCHAR(20)) + ' has no item for package: ' + @packageName + ' item:' + @iDefName + ' qty:' + CAST(@iQty AS VARCHAR(20)));
			END
		END
        ELSE
        BEGIN
        	
			PRINT ('item is a spark unlocker: ' + @iDefName);
			EXEC dbo.accountPackageGenerateSpark @accountId, @iDef
        END


		FETCH NEXT FROM packItems INTO @iDef, @iQty;
	END
	CLOSE packItems; DEALLOCATE packItems;
	   
	
END
GO