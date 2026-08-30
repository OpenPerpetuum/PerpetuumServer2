/****** Object:  UserDefinedTableType [dbo].[integer_list_tbltype]    Script Date: 10.05.2026 7:29:35 ******/
CREATE TYPE [dbo].[integer_list_tbltype] AS TABLE(
	[n] [int] NOT NULL,
	PRIMARY KEY CLUSTERED 
(
	[n] ASC
)WITH (IGNORE_DUP_KEY = OFF)
)
GO