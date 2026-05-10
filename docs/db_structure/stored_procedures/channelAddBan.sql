/****** Object:  StoredProcedure [dbo].[channelAddBan]    Script Date: 10.05.2026 13:23:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[channelAddBan]
(
	@memberid as int,
	@channelid as int
)
AS
BEGIN

if not exists (select id from channelbans where memberid = @memberid and channelid = @channelid)
begin
	insert into channelbans (memberid,channelid) values (@memberid,@channelid)
end

END
GO