/****** Object:  StoredProcedure [dbo].[characterSettingsSetString]    Script Date: 10.05.2026 13:27:42 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


create PROCEDURE [dbo].[characterSettingsSetString] 
	(
	@characterid int,
	@data nvarchar(max)
	)
	
AS
	SET NOCOUNT ON
	
	if not exists ( select characterid from charactersettings where characterid=@characterid )
	begin
		insert charactersettings (characterid, settingsstring) values (@characterid, @data)
	end
	else
	begin
		update charactersettings set settingsstring=@data where characterid=@characterid
	end
	
	
	
	RETURN


GO