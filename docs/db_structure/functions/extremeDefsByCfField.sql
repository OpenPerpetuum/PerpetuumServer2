/****** Object:  UserDefinedFunction [dbo].[extremeDefsByCfField]    Script Date: 10.05.2026 10:30:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[extremeDefsByCfField] 
(
	@definitions IntList READONLY,
	@fieldId INT,
	@returnMax BIT
)
RETURNS VARCHAR(100)
AS
BEGIN
DECLARE @maxName VARCHAR(100),@minName VARCHAR(100);

SELECT TOP 1 @maxName=dbo.GetDefinitionName(ar.definition)
FROM dbo.aggregateRecordsByIds(@definitions) ar WHERE ar.field=@fieldId ORDER BY ar.value DESC;

SELECT TOP 1 @minName=dbo.GetDefinitionName(ar.definition)
FROM dbo.aggregateRecordsByIds(@definitions) ar WHERE ar.field=@fieldId ORDER BY ar.value asc;


IF (@returnMax=1)
BEGIN
    RETURN @maxName;
END

RETURN @minName;


END



GO