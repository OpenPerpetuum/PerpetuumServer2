/****** Object:  UserDefinedFunction [dbo].[getDefinitionByCategoryflag]    Script Date: 10.05.2026 9:55:49 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE FUNCTION [dbo].[getDefinitionByCategoryflag]
(	
	@categoryflag bigint,
	@mask bigint
)
returns @t2 table (definition int)

AS
begin

insert into @t2

SELECT definition FROM dbo.entitydefaults WHERE (categoryflags & @mask) = @categoryflag

return
end
GO