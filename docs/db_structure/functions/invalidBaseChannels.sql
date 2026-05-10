/****** Object:  UserDefinedFunction [dbo].[invalidBaseChannels]    Script Date: 10.05.2026 10:06:55 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


--nem letezo bazisokhoz channel
CREATE FUNCTION [dbo].[invalidBaseChannels]
(	
	
)
RETURNS TABLE 
AS
RETURN 
(
select id from channels where name like 'base_%' and id not in
(
select c.id from channels c 
join entities e on c.name = ('base_' +  cast(e.eid as varchar(50)))
where c.name like 'base_%'
)
)
GO