/****** Object:  UserDefinedFunction [dbo].[GetTableStats]    Script Date: 10.05.2026 10:05:01 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


create FUNCTION [dbo].[GetTableStats] 
(	
	@tableName VARCHAR(128)
)
RETURNS TABLE 
AS
RETURN 
(
	SELECT name AS stats_name, 
    STATS_DATE(object_id, stats_id) AS updtime
FROM sys.stats 
WHERE object_id = OBJECT_ID( REPLACE(@tableName,'dbo.',''))

)
GO