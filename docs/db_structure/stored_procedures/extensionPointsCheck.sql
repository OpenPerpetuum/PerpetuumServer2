/****** Object:  StoredProcedure [dbo].[extensionPointsCheck]    Script Date: 10.05.2026 15:22:04 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[extensionPointsCheck] 
	
	@now DATETIME 

AS
BEGIN
	SET NOCOUNT ON;

	
DECLARE @year INT, @month INT, @day INT

   SET @year = DATEPART(yy, @now ) 
   SET @month = DATEPART(mm, @now) 
   SET @day = DATEPART(dd, @now)

SELECT COUNT(*) FROM dbo.extensionpointworklog WHERE 
DATEPART(yy, eventtime) = @year AND
DATEPART(mm, eventtime) = @month AND
DATEPART(dd, eventtime) = @day

END
GO