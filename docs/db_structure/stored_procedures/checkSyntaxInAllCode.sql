/****** Object:  StoredProcedure [dbo].[checkSyntaxInAllCode]    Script Date: 10.05.2026 13:30:34 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[checkSyntaxInAllCode]
	
AS
BEGIN

SET NOCOUNT ON;

-- check for table existence 
-- since i pimped it the first piece of code
-- seems to do the same as the second one, but who knows...


SELECT s.name, p.name, db = COALESCE(d.referenced_database_name, DB_NAME()),
  [schema] = d.referenced_schema_name, [object] = d.referenced_entity_name
  FROM sys.schemas AS s
  INNER JOIN dbo.allCodingObjects() AS p
  ON s.[schema_id] = p.[schema_id]
  INNER JOIN sys.sql_expression_dependencies AS d
  ON p.[object_id] = d.referencing_id
  WHERE d.referenced_id IS NULL
    AND d.referenced_server_name IS NULL
    AND d.referenced_class = 1
    AND OBJECT_ID(QUOTENAME(COALESCE(d.referenced_database_name, DB_NAME()))
          + '.' + QUOTENAME(d.referenced_schema_name)
          + '.' + QUOTENAME(d.referenced_entity_name)) IS NULL

--check for columns existence


--create temp table containing all sp, functions, and views we need to check
 IF EXISTS (SELECT * FROM tempdb.dbo.sysobjects o WHERE o.xtype in ('U') and o.id = object_id(N'tempdb..#tempIdentityTables'))
 DROP TABLE #tempIdentityTables
 
CREATE TABLE #tempIdentityTables(
 id int not null,
 name [varchar](255) NOT NULL,
 identOn int default (0),
 ansiNullOn int default (0),
 [owner] VARCHAR(255),
 [objectType] VARCHAR(255),
 [createdDate] datetime,
 [definition] VARCHAR(MAX), --definition
 command VARCHAR(MAX), --definition
 results VARCHAR(MAX),
 processed int default (0),
 )
 
-- populate it with sp, views, and functions to be checked
 insert into #tempIdentityTables (id, name, identOn, ansiNullOn, [owner], [objectType], [createdDate], [definition])
 select
 o.id,
 o.name,
 OBJECTPROPERTY(id, 'ExecIsQuotedIdentOn') as quoted_ident_on,
 OBJECTPROPERTY(id, 'ExecIsAnsiNullsOn') as ansi_nulls_on,
 user_name(o.uid) owner ,
 --o.type,
 pr.type_desc ,
 pr.create_date ,
 mod.definition
 from sysobjects o
 INNER JOIN sys.sql_modules mod ON o.id = mod.object_id
 inner join
 ( select object_id, name, type_desc,create_date from sys.views
 union
 select object_id, name, type_desc,create_date from sys.procedures
 union
 SELECT object_id, name, type_desc,create_date FROM sys.objects WHERE type IN ('FN', 'IF', 'TF','AF') -- scalar, inline table-valued, table-valued
 ) as pr on pr.object_id = o.id
 where category = 0
 
-- create the exec commands ready for the parse checks (unnessasary but good for debuggin, script intersection, code resuse)
 DECLARE @id int
 DECLARE @name VARCHAR(255)
 DECLARE @owner VARCHAR(255)
 DECLARE @identOn int
 DECLARE @ansiNullOn int
 DECLARE @SQL VARCHAR(MAX)
 DECLARE @definition VARCHAR(MAX)
 --set start condition cycle
 SELECT TOP 1 @id = id, @name = name , @owner = [owner], @identOn = identOn, @ansiNullOn = ansiNullOn, @definition =[definition] FROM #tempIdentityTables WHERE processed = 0
 
WHILE @id IS NOT NULL
 BEGIN
 set @SQL = ''
 
--if (@ansiNullOn > 0)
 --SELECT @SQL = @SQL +'SET ANSI_NULLS ON ' + CHAR(13)+CHAR(10)
 
--if (@identOn > 0)
 --SELECT @SQL = @SQL +'SET QUOTED_IDENTIFIER ON ' + CHAR(13)+CHAR(10)
 SELECT @SQL = @SQL +'SET FMTONLY ON ' + CHAR(13)+CHAR(10)
 --SELECT @SQL = @SQL +'SET NOEXEC ON ' + CHAR(13)+CHAR(10)
 
SELECT @SQL = @SQL + ' EXECUTE ('' '+ REPLACE(@definition,'''', '''''') +' '')' + CHAR(13)+CHAR(10)
 
--SELECT @SQL = @SQL +'SET NOEXEC OFF ' + CHAR(13)+CHAR(10)
 --SELECT @SQL = @SQL +'SET PARSEONLY OFF ' + CHAR(13)+CHAR(10)
 
-- mark as processed
 update #tempIdentityTables
 set command = @SQL, processed = 1
 where #tempIdentityTables.id = @id
 
--if no select the name would be last value and we'd get into an endless loop.... grrr
 set @id = null;
 SELECT TOP 1 @id = id, @name = name , @owner = [owner], @identOn = identOn, @ansiNullOn = ansiNullOn, @definition =[definition] FROM #tempIdentityTables WHERE processed = 0
 
END
 
-- run actual queries
 DECLARE @loopId int
 DECLARE @ExecSQL NVARCHAR(MAX)
 DECLARE @procName VARCHAR(255) = null
 DECLARE @objectType VARCHAR(255) = null
 
set @id = null
 select top 1 @loopId = id, @ExecSQL = command, @procName = name, @objectType = objectType from #tempIdentityTables where processed = 0 OR processed = -1 OR processed = 1
 
WHILE @loopId IS NOT NULL
 BEGIN
 
BEGIN TRY
 print 'Checking: ' + @procName
 EXEC sp_executesql @ExecSQL
 
update #tempIdentityTables
 set processed = 2
 where #tempIdentityTables.id = @loopId
 END TRY
 BEGIN CATCH
 
DECLARE @Failure varchar(MAX) = ''
 select @Failure = ERROR_MESSAGE();
 print 'Error detected: ' + @Failure
 update #tempIdentityTables
 set processed = -2,
 results = @Failure
 where #tempIdentityTables.id = @loopId
 END CATCH;
 
--if no results then name would be last value and we'd get into an endless loop.... grrr
 set @loopId = null;
 select top 1 @loopId = id, @ExecSQL = command, @procName = name, @objectType = objectType from #tempIdentityTables where processed = 0 OR processed = -1 OR processed = 1
 
END
 -- results returned are queries with errors
 select results, * from #tempIdentityTables where results is not null




END
GO