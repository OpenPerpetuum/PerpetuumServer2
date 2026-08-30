/****** Object:  UserDefinedFunction [dbo].[padLeft]    Script Date: 10.05.2026 10:52:41 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

Create    function [dbo].[padLeft]
(
@value varchar(200),
@padChar char(1)='0',
@len INT=2
)
returns varchar(300)
    as
    Begin
      return replicate(@PadChar,@len-Len(@value))+@value
    end
GO