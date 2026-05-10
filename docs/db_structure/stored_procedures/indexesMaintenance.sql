/****** Object:  StoredProcedure [dbo].[indexesMaintenance]    Script Date: 10.05.2026 16:37:15 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[indexesMaintenance]
AS
BEGIN

	SET NOCOUNT ON

	CREATE TABLE #Fragmentation 
	(
		TableName NVARCHAR(200),
		IndexName NVARCHAR(200),
		FragmentationAmount DECIMAL(18,4)
	)

	-- Load all of the fragmented tables
	INSERT INTO #Fragmentation (TableName, IndexName, FragmentationAmount)
		SELECT  DISTINCT 
			TableName = S.name + '.' + tbl.[name],
			IndexName = ind.name,
			FragmentationAmount = MAX(mn.avg_fragmentation_in_percent)
		FROM sys.dm_db_index_physical_stats(NULL, NULL, NULL, NULL, NULL) AS mn
			INNER JOIN sys.tables tbl ON tbl.[object_id] = mn.[object_id]
			INNER JOIN sys.indexes ind ON ind.[object_id] = mn.[object_id]
			INNER JOIN sys.schemas S ON tbl.schema_id = S.schema_id
		WHERE [database_id] = DB_ID() AND
			mn.avg_fragmentation_in_percent > 5 AND
			ind.type_desc <> 'NONCLUSTERED COLUMNSTORE' AND
			ind.name IS NOT NULL
		GROUP BY S.name + '.' + tbl.[name], ind.name
		ORDER BY MAX(mn.avg_fragmentation_in_percent) DESC

	DECLARE @tableName NVARCHAR(200)
	DECLARE @indexName NVARCHAR(200)
	DECLARE @fragmentationAmount DECIMAL(18,4)
	DECLARE @sql VARCHAR(1000)

	DECLARE curse CURSOR FAST_FORWARD READ_ONLY FOR
		SELECT TableName, IndexName, FragmentationAmount FROM #Fragmentation

	OPEN curse

	FETCH NEXT FROM curse INTO @tableName, @indexName, @fragmentationAmount

	WHILE @@FETCH_STATUS = 0
	BEGIN
		SET @sql = 'ALTER INDEX ' + @IndexName + ' ON ' + @TableName + CASE WHEN @FragmentationAmount > 30 THEN ' REBUILD' ELSE ' REORGANIZE' END

		PRINT @sql
		EXEC(@sql)

		FETCH NEXT FROM curse INTO @tableName, @indexName, @fragmentationAmount
	END

	CLOSE curse
	DEALLOCATE curse

	DROP TABLE #Fragmentation

END

GO