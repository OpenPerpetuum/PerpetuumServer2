/****** Object:  StoredProcedure [dbo].[increaseExtensionLevel]    Script Date: 10.05.2026 16:31:42 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[increaseExtensionLevel]
(
	@characterID as int,
	@extensionID as INT,
	@extensionLevel AS INT
)
AS
BEGIN


if exists (select characterextensionid from characterExtensions where characterid = @characterID and extensionID = @extensionID)
begin
	update characterextensions set extensionlevel = @extensionLevel  where characterid = @characterID and extensionid = @extensionID
end 
else
begin
	insert into characterextensions (characterid,extensionid,extensionlevel) values (@characterID,@extensionID,1)
end

select @@ROWCOUNT


END


GO