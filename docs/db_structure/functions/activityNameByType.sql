/****** Object:  UserDefinedFunction [dbo].[activityNameByType]    Script Date: 10.05.2026 10:16:28 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


/*
		Undefined =0,
        Gathering,  // mining / harvesting
        Mission,  // any mission objective
        Production, // any production
        Artifact, 
        Intrusion,
        Npc

*/


CREATE FUNCTION [dbo].[activityNameByType] 
(
	@activityType int
)
RETURNS VARCHAR(20)
AS
BEGIN
	DECLARE @name VARCHAR(20);




	IF (@activityType =0)
	BEGIN
		SET @name ='Undefined';
	END
    ELSE IF (@activityType =1)
	BEGIN
	    SET @name = 'Gathering';
	END 
	ELSE IF (@activityType =2)
	BEGIN
	    SET @name = 'Mission';
	END
	ELSE IF (@activityType =3)
	BEGIN
	    SET @name = 'Production';
	END
	ELSE IF (@activityType =4)
	BEGIN
	    SET @name = 'Artifact';
	END
	ELSE IF (@activityType =5)
	BEGIN
	    SET @name = 'Intrusion';
	END
	ELSE IF (@activityType =6)
	BEGIN
	    SET @name = 'Npc';
	END


	RETURN @name;


END
GO