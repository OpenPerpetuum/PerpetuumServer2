/****** Object:  UserDefinedFunction [dbo].[allPossibleOwners]    Script Date: 10.05.2026 9:52:01 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

 
CREATE FUNCTION [dbo].[allPossibleOwners] 
( )
RETURNS TABLE 
AS
RETURN 
(
	 
SELECT eid FROM dbo.corporations
UNION
SELECT rooteid FROM dbo.characters WHERE rootEID > 0
)

GO