/****** Object:  StoredProcedure [dbo].[cleanUpZoneUserEntities]    Script Date: 10.05.2026 13:34:34 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[cleanUpZoneUserEntities] 
	
AS
BEGIN
	
	SET NOCOUNT ON;

   delete dbo.zoneuserentities WHERE eid NOT IN (SELECT eid FROM dbo.entities)
   SELECT @@ROWCOUNT
END
GO