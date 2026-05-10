/****** Object:  StoredProcedure [dbo].[addpresettorandompool]    Script Date: 10.05.2026 7:47:17 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[addpresettorandompool]
	
	@presetID int, 
	@presenceID int 
AS
BEGIN
	
	
	SET NOCOUNT ON;

    INSERT npcrandomflockpool (presenceid, flockid, rate) 
    SELECT @presenceID,flockid,rate FROM npcpoolpresetvalues WHERE presetid=@presetID
	
END
GO