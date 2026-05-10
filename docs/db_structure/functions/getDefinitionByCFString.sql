/****** Object:  UserDefinedFunction [dbo].[getDefinitionByCFString]    Script Date: 10.05.2026 9:56:57 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

create FUNCTION [dbo].[getDefinitionByCFString]
(	
	@cfString VARCHAR(128)
	
)
returns @t2 table (definition int)

AS
begin
DECLARE @mask BIGINT, @categoryflag BIGINT

SET @categoryflag = (SELECT [value] FROM dbo.categoryFlags WHERE name=@cfString)

SET @mask = dbo.GetCFMask(@categoryflag)
insert into @t2
SELECT definition FROM dbo.entitydefaults WHERE (categoryflags & @mask) = @categoryflag

return
end
GO