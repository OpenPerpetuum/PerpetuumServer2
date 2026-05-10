/****** Object:  StoredProcedure [dbo].[copycharacterwizard]    Script Date: 10.05.2026 13:36:28 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[copycharacterwizard]
	
AS
BEGIN
	
	
	SET NOCOUNT ON;

    
	DELETE dbo.cw_major_extension WHERE majorid>9

INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 10,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=1

INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 19,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=1



INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 11,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=2

INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 20,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=2



INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 12,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=3

INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 21,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=3


INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 13,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=4

INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 22,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=4



INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 14,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=5

INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 23,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=5




INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 15,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=6

INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 24,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=6




INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 16,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=7

INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 25,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=7



INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 17,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=8

INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 26,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=8



INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 18,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=9

INSERT dbo.cw_major_extension (	majorid,extensionid,levelincrement)
SELECT 27,extensionid,levelincrement FROM dbo.cw_major_extension WHERE majorid=9





DELETE dbo.cw_school_extension WHERE schoolid>3

INSERT dbo.cw_school_extension (schoolid,extensionid,levelincrement)
SELECT 4, extensionid,levelincrement FROM dbo.cw_school_extension WHERE schoolid=1

INSERT dbo.cw_school_extension (schoolid,extensionid,levelincrement)
SELECT 7, extensionid,levelincrement FROM dbo.cw_school_extension WHERE schoolid=1


INSERT dbo.cw_school_extension (schoolid,extensionid,levelincrement)
SELECT 5, extensionid,levelincrement FROM dbo.cw_school_extension WHERE schoolid=2

INSERT dbo.cw_school_extension (schoolid,extensionid,levelincrement)
SELECT 8, extensionid,levelincrement FROM dbo.cw_school_extension WHERE schoolid=2


INSERT dbo.cw_school_extension (schoolid,extensionid,levelincrement)
SELECT 6, extensionid,levelincrement FROM dbo.cw_school_extension WHERE schoolid=3

INSERT dbo.cw_school_extension (schoolid,extensionid,levelincrement)
SELECT 9, extensionid,levelincrement FROM dbo.cw_school_extension WHERE schoolid=3












END
GO