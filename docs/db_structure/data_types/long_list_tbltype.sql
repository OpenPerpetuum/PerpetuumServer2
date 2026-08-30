/****** Object:  UserDefinedTableType [dbo].[long_list_tbltype]    Script Date: 10.05.2026 7:31:05 ******/
CREATE TYPE [dbo].[long_list_tbltype] AS TABLE(
	[n] [bigint] NOT NULL,
	PRIMARY KEY CLUSTERED 
(
	[n] ASC
)WITH (IGNORE_DUP_KEY = OFF)
)
GO