/****** Object:  StoredProcedure [dbo].[getTableColumnInfo]    Script Date: 10.05.2026 16:29:28 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[getTableColumnInfo]
	(
    @table_name varchar(384)
	)
	
	--retrieves all info about the specified table's columns
	
AS
	SET NOCOUNT ON;
		
	exec sp_columns @table_name
	
GO