/****** Object:  StoredProcedure [dbo].[DeleteAllGang]    Script Date: 10.05.2026 14:02:21 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[DeleteAllGang]
AS
BEGIN
delete channelmembers where channelid in( select id from channels where type = 3)
delete channels where type = 3
delete gangmembers
delete gang
END

GO