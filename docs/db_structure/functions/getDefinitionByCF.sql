/****** Object:  UserDefinedFunction [dbo].[getDefinitionByCF]    Script Date: 10.05.2026 9:56:24 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

create FUNCTION [dbo].[getDefinitionByCF]
(	
	@categoryflag bigint
	
)
returns @t2 table (definition int)

AS
begin
DECLARE @mask BIGINT
SET @mask = dbo.GetCFMask(@categoryflag)
insert into @t2
SELECT definition FROM dbo.entitydefaults WHERE (categoryflags & @mask) = @categoryflag

return
end
GO