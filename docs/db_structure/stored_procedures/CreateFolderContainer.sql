/****** Object:  StoredProcedure [dbo].[CreateFolderContainer]    Script Date: 10.05.2026 13:40:59 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[CreateFolderContainer]
	@owner BIGINT ,
	@parent BIGINT ,
	@result BIGINT OUTPUT
	
AS
BEGIN
	
DECLARE @folderEid BIGINT

SET @folderEid = (SELECT dbo.TryGetRandomEid())

--create folder def_infinite_capacity_box_container (577) o:967 p:680 h:100 n: q:1 r:False
INSERT dbo.entities
        ( eid ,
          definition ,
          owner ,
          parent ,
          health ,
          ename ,
          quantity ,
          repackaged ,
          dynprop
        )
VALUES  ( @folderEid , -- eid - bigint
          577 , -- definition - int
          @owner , -- owner - bigint
          @parent , -- parent - bigint
          100 , -- health - float
          N'reimbursed items ' + CAST(GETDATE() AS VARCHAR(64)) , -- ename - nvarchar(128)
          1 , -- quantity - int
          0 , -- repackaged - bit
          NULL
        )

		--visszamegy az uj eid
		SET @result = @folderEid

RETURN 
		
END
GO