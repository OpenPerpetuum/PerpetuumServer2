/****** Object:  StoredProcedure [dbo].[artifactReset]    Script Date: 10.05.2026 7:49:10 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[artifactReset] 
	
AS
BEGIN

	SET NOCOUNT ON;
	DECLARE @fromDate DATETIME;
SET @fromDate= DATEADD(DAY,-3,GETDATE());
SELECT @fromDate;

DELETE dbo.artifacts WHERE created<@fromDate
 
END
GO