/****** Object:  StoredProcedure [dbo].[cleanDisabledDefinitions]    Script Date: 10.05.2026 13:31:18 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[cleanDisabledDefinitions]
	
	
	
AS
BEGIN
	
	
	SET NOCOUNT ON;

    DELETE dbo.entities WHERE definition in (SELECT definition FROM dbo.entitydefaults WHERE enabled=0)
	
END
GO