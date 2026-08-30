/****** Object:  UserDefinedFunction [dbo].[counticeingame]    Script Date: 10.05.2026 10:20:52 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[counticeingame] 
(
	
)
RETURNS int
AS
BEGIN
	
	
	RETURN (SELECT SUM(quantity) FROM entities WHERE definition=5202)

END
GO