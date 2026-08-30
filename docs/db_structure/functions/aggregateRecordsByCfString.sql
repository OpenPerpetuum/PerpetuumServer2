/****** Object:  UserDefinedFunction [dbo].[aggregateRecordsByCfString]    Script Date: 10.05.2026 9:49:36 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[aggregateRecordsByCfString] 
(
	@cfString VARCHAR(128)
)
RETURNS 
@result TABLE 
(
	[definition] INT,
	[field] INT,
	[value] FLOAT,
	[definitionname] VARCHAR(100),
	[fieldname] nvarchar(100),
	[moreisbetter] BIT,
	[increasing] VARCHAR(5)
)
AS
BEGIN

	DECLARE @definitions IntList;
	INSERT @definitions    ( idval )
	SELECT definition FROM dbo.getDefinitionByCFString(@cfString);

	INSERT @result
	        ( [definition], [field], [value], [definitionname], [fieldname], moreisbetter, increasing )
	SELECT r.[definition], r.[field],r.[value], r.[definitionname],r.[fieldname],r.[moreisbetter],r.[increasing] FROM dbo.aggregateRecordsByIds(@definitions) r;
	
	RETURN 
END
GO