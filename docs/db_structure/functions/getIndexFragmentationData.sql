/****** Object:  UserDefinedFunction [dbo].[getIndexFragmentationData]    Script Date: 10.05.2026 9:59:36 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getIndexFragmentationData] 
(	
	@threshold int 
)
RETURNS TABLE 
AS
RETURN 
(
	
	  
SELECT 
	b.name,
	a.avg_fragmentation_in_percent as fp,
	OBJECT_NAME(b.object_id) AS tablename,
	'ALTER INDEX '+ cast(b.name as varchar(50))+' ON ' + OBJECT_NAME(b.object_id) + ' REBUILD ' 
		AS todo

FROM sys.dm_db_index_physical_stats (DB_ID(), NULL, NULL, NULL, NULL) AS a
    JOIN sys.indexes AS b ON a.object_id = b.object_id AND a.index_id = b.index_id
	where 
	a.avg_fragmentation_in_percent > @threshold
	and
	b.name is not null
	
)
GO