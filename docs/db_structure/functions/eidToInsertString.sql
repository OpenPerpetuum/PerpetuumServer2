/****** Object:  UserDefinedFunction [dbo].[eidToInsertString]    Script Date: 10.05.2026 10:25:41 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[eidToInsertString] 
(
	@eid bigint
)
RETURNS VARCHAR(MAX)
AS
BEGIN
	
	DECLARE @Result VARCHAR(MAX)

	SET @Result = 
(
SELECT 
'('
+ dbo.intToInsertString([eid]) + ', '
+ dbo.intToInsertString([definition]) + ', '
+ dbo.intToInsertString([owner]) + ', '
+ dbo.intToInsertString([parent]) + ', '
+ dbo.intToInsertString([health]) + ', '
+ dbo.stringToInsertString([ename]) + ', '
+ dbo.intToInsertString([quantity]) + ', '
+ dbo.intToInsertString([repackaged]) + ', '
+ dbo.stringToInsertString([dynprop]) 
+ '),'
  FROM dbo.entities WHERE eid=@eid
)

	SET @Result = RTRIM(LTRIM(@Result))
	
	RETURN @Result
	
END
GO