/****** Object:  StoredProcedure [dbo].[debug_disablenpcs]    Script Date: 10.05.2026 13:45:46 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[debug_disablenpcs] 
	
AS
BEGIN
	SET NOCOUNT ON;
	

UPDATE npcpresence SET enabled = 0 --ALL off
UPDATE npcpresence SET enabled = 1 WHERE presencetype=2 OR presencetype=4 --dynamic on, dynpool on
    
END
GO