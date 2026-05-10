/****** Object:  StoredProcedure [dbo].[entitiesReportAndDeleteOrphanedByCf]    Script Date: 10.05.2026 15:09:14 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[entitiesReportAndDeleteOrphanedByCf]
	@cfString VARCHAR(512),
	@run INT = 0
AS
BEGIN


----check by cf string
SELECT COUNT(*),@cfString FROM dbo.entities WHERE definition in
(SELECT definition FROM dbo.getDefinitionByCFString(@cfString))
AND
parent IS NOT NULL AND parent NOT IN (SELECT eid FROM dbo.entities)


IF (@run =0)
BEGIN
	RETURN
END

--delete by cf
DELETE dbo.entities WHERE definition IN 
(SELECT definition FROM dbo.getDefinitionByCFString(@cfString))
AND
parent IS NOT NULL AND parent NOT IN (SELECT eid FROM dbo.entities)






END
GO