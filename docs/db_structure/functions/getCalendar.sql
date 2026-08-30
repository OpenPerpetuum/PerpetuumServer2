/****** Object:  UserDefinedFunction [dbo].[getCalendar]    Script Date: 10.05.2026 9:53:50 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE FUNCTION [dbo].[getCalendar]
(	
	@days int
)
returns @t2 table (dt datetime)

AS
begin

declare @startDate as smalldatetime

set @startDate = convert(smalldatetime,convert(char(8),dateadd(day,-(@days - 1),getdate()),112))

insert into @t2
select top(@days) *
from
(
select @startDate + n3.num * 100 + n2.num * 10 +n1.num as n 
from
(
	select 0 as num union all
	select 1 union all
	select 2 union all
	select 3 union all
	select 4 union all
	select 5 union all
	select 6 union all
	select 7 union all
	select 8 union all
	select 9
) n1,
(
	select 0 as num union all
	select 1 union all
	select 2 union all
	select 3 union all
	select 4 union all
	select 5 union all
	select 6 union all
	select 7 union all
	select 8 union all
	select 9
) n2,
(
	select 0 as num union all
	select 1 union all
	select 2 union all
	select 3
) n3
) gencalendar
order by 1

return
end
GO