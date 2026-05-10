/****** Object:  StoredProcedure [dbo].[debug_disablezones]    Script Date: 10.05.2026 13:46:21 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[debug_disablezones] 
	
AS
BEGIN
	SET NOCOUNT ON;
UPDATE dbo.zones SET enabled = 0   
END
GO