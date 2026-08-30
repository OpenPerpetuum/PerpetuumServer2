/****** Object:  UserDefinedFunction [dbo].[getDockingbaseDefinitions]    Script Date: 10.05.2026 9:58:11 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



CREATE FUNCTION [dbo].[getDockingbaseDefinitions] ()
RETURNS TABLE 
AS
RETURN 
(
SELECT [definition] FROM dbo.getDefinitionByCF(65912)
union
SELECT [definition] FROM dbo.getDefinitionByCF(151192722)
)
GO