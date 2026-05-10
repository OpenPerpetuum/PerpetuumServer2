/****** Object:  StoredProcedure [dbo].[accountPackageGenerateAll]    Script Date: 10.05.2026 7:37:29 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[accountPackageGenerateAll]
	@accountId int  
AS
BEGIN
	
	SET NOCOUNT ON;

    DECLARE @packIdList integer_list_tbltype, @packageName VARCHAR(512);;

	INSERT @packIdList ( n ) VALUES
	( 1 ), ( 2 ), ( 3 ), ( 4 ), ( 5 );
	
	DECLARE @packId INT;
	DECLARE packIds CURSOR LOCAL FORWARD_ONLY READ_ONLY FOR SELECT n FROM @packIdList;
	OPEN packIds
	FETCH NEXT FROM packIds INTO @packId;
	WHILE( @@FETCH_STATUS =0)
	BEGIN
		SET @packageName = (SELECT name FROM dbo.packages WHERE id=@packId);
		PRINT ('processing package:' + CAST(@packId AS VARCHAR(20)) + ' ' + @packageName);

		IF (dbo.accountPackageIsPurchased(@accountId, @packId)=1)
		BEGIN
			EXEC dbo.accountPackageProcessOne @accountId, @packId;	
		END

		FETCH NEXT FROM packIds INTO @packId;
	END
	
	CLOSE packIds; DEALLOCATE packIds;



END
GO