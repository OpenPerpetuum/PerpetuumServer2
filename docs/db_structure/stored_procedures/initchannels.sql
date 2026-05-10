/****** Object:  StoredProcedure [dbo].[initchannels]    Script Date: 10.05.2026 16:37:53 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[initchannels]
	
AS
BEGIN
	
	
SET NOCOUNT ON;
	
--delete all channels
DELETE dbo.channels

--create corporation channels
INSERT channels ([name],[password],[topic],[type])

SELECT 'corporation_' +  CAST(c.eid AS VARCHAR(16)), 
NULL,
w.missionstatement,
2
FROM corporations c left JOIN dbo.cw_corporation w ON c.eid=w.corporationEID WHERE c.active=1 

--create station channels
INSERT channels ([name],[password],[topic],[type]) 
SELECT 'base_' +  CAST(eid AS VARCHAR(16)),
NULL,
NULL,
4
FROM dbo.zoneentities WHERE eid in  (SELECT eid FROM dbo.entities WHERE definition in (SELECT definition FROM dbo.entitydefaults where definitionname LIKE '%docking%'))

TRUNCATE TABLE dbo.channelmembers

--create corp channel members
INSERT dbo.channelmembers (	memberid, channelid,[role]) 
SELECT memberid,
(SELECT id FROM channels WHERE [name]= 'corporation_' +  CAST(cm.corporationEID AS VARCHAR(16) )),
(CASE ([role] & 19) WHEN 0 THEN 0 ELSE 2 end)
from dbo.corporationmembers cm JOIN characters c ON cm.memberid = c.characterID WHERE c.active=1

--station channel members
INSERT dbo.channelmembers (	memberid, channelid,[role]) 
SELECT (SELECT id FROM channels WHERE [name] ='base_' +  CAST(baseEID AS VARCHAR(16))),
characterid,
0
FROM characters WHERE active=1


END
GO