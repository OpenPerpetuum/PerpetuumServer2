/****** Object:  StoredProcedure [dbo].[debug_enablenpcs]    Script Date: 10.05.2026 13:47:45 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[debug_enablenpcs] 
	
AS
BEGIN
	SET NOCOUNT ON;
	

UPDATE npcpresence SET enabled = 1
UPDATE npcpresence SET enabled=0 WHERE [name]='random_flock_gatherer'
update npcpresence set enabled=0 where [name]='debug_presence'
   
END
GO