/****** Object:  StoredProcedure [dbo].[extensionPointsInject]    Script Date: 10.05.2026 15:24:03 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[extensionPointsInject] 
	
	@accountId int , 
	@points int 
	AS
BEGIN
	
	SET NOCOUNT ON;

    insert extensionpoints (accountid,points) values (@accountId,@points)


END
GO