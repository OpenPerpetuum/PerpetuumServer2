/****** Object:  StoredProcedure [dbo].[getExtensionBonus]    Script Date: 10.05.2026 16:02:11 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[getExtensionBonus]
	@characterid int
AS
BEGIN
	SET NOCOUNT ON;

	select extensions.targetpropertyid as field,
		   sum(extensions.bonus * characterextensions.extensionlevel) as bonus
	from characterextensions inner join extensions on characterextensions.extensionid = extensions.extensionid 
	where characterid = @characterid and extensions.targetpropertyid is not null  group by extensions.targetpropertyid
END
GO