/****** Object:  UserDefinedFunction [dbo].[TryGetRandomEid]    Script Date: 10.05.2026 10:58:18 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[TryGetRandomEid] 
()
RETURNS bigint
AS
BEGIN
	
	DECLARE @Result bigint

	SET @Result = dbo.GetRandomEid()
	
	WHILE (SELECT COUNT(*) FROM dbo.entities WHERE eid=@Result) > 0
	BEGIN
		SET @Result = dbo.GetRandomEid()
	END  
  	
	RETURN @Result

END
GO