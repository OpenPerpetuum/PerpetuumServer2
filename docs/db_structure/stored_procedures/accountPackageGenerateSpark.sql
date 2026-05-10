/****** Object:  StoredProcedure [dbo].[accountPackageGenerateSpark]    Script Date: 10.05.2026 7:38:14 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[accountPackageGenerateSpark] 
	
	@accountId int, 
	@sparkActivatorDefinition int
AS
BEGIN
	
	SET NOCOUNT ON;

	DECLARE @defName VARCHAR(512);
	SET @defName = dbo.GetDefinitionName(@sparkActivatorDefinition);

	DECLARE @nofCurrentRedeemableSpark INT;
	IF EXISTS ( SELECT 1 FROM dbo.accountredeemableitems WHERE accountid=@accountId AND [definition]=@sparkActivatorDefinition AND wasredeemed=0)
	BEGIN
		-- has unredeemed 
		PRINT (' accountId:' + CAST(@accountId AS VARCHAR(20)) + ' has unredeemed sparkActivator:' + CAST(@sparkActivatorDefinition AS VARCHAR(20)) + ' ' + @defName);
		RETURN

	END


	DECLARE @activeCharacters integer_list_tbltype, @nofChararcters INT;

	INSERT @activeCharacters ( n )
	SELECT characterID FROM characters WHERE accountID=@accountId AND active=1;
	
	SET @nofChararcters = (SELECT COUNT(*) FROM @activeCharacters);

	DECLARE @nofActivated INT;
	SET @nofActivated = (SELECT COUNT(*) FROM dbo.accountredeemableitems WHERE accountid=@accountId AND [definition]=@sparkActivatorDefinition AND characterid IN (SELECT n FROM @activeCharacters));

	PRINT('activations:' + CAST(@nofActivated AS VARCHAR(20)) + ' characters:' + CAST(@nofChararcters AS VARCHAR(20)));

	IF (@nofChararcters > @nofActivated OR @nofChararcters=0)
	BEGIN
		
		INSERT dbo.accountredeemableitems
				( accountid , [definition] , quantity )
		VALUES  ( @accountId,  @sparkActivatorDefinition,         1   )

		PRINT ( 'spark activator inserted for accountId:'+CAST(@accountId AS VARCHAR(20)) + ' - '+ CAST(@sparkActivatorDefinition AS VARCHAR(20)) + ' ' + @defName );
		RETURN;
	END


    
	PRINT( 'enough activators for accountId:'+CAST(@accountId AS VARCHAR(20)) + ' - '+ CAST(@sparkActivatorDefinition AS VARCHAR(20)) + ' ' + @defName );
END
GO