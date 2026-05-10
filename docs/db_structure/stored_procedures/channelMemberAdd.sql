/****** Object:  StoredProcedure [dbo].[channelMemberAdd]    Script Date: 10.05.2026 13:25:18 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[channelMemberAdd]
(
	@channelid as int,
	@memberid as int,
	@role as int
)
AS
BEGIN

if not exists (select id from channelmembers where channelid = @channelid and memberid = @memberid)
begin
	insert into channelmembers (channelid,memberid,[role]) values (@channelid,@memberId,@role)
end

select @@ROWCOUNT

END
GO