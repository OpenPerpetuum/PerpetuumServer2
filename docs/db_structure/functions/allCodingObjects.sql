/****** Object:  UserDefinedFunction [dbo].[allCodingObjects]    Script Date: 10.05.2026 9:51:26 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[allCodingObjects] 
(	
	
)
RETURNS TABLE 
AS
RETURN 
(
	SELECT a.object_id,a.schema_id, a.Name AS name
FROM sys.objects a
INNER JOIN sys.schemas b
ON a.schema_id = b.schema_id
WHERE TYPE in ('FN', 'IF', 'TF','AF', 'P' ,'V') 
)
GO