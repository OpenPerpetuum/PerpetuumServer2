/****** Object:  UserDefinedFunction [dbo].[showDynProp]    Script Date: 10.05.2026 10:56:17 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


/*
offsetWithinDay=i5
armor=tD7BDF13E
constructionLevelCurrent=i1f4
creation=d2015.2.20.13.46.15
reinforceCounter=i2
nextReinforceIncrease=d2015.12.23.2.15.49
constructionDirection=i1
isOnline=i1
isReinforced=i1
reinforceEnd=d2015.12.23.2.14.49
*/


CREATE FUNCTION [dbo].[showDynProp] 
(
	@dynProp VARCHAR(max) 
)
RETURNS VARCHAR(max)
AS
BEGIN
	
	DECLARE @result VARCHAR(max);
	DECLARE @piece VARCHAR(max);
	SET @result = '';

    DECLARE pieces CURSOR LOCAL FORWARD_ONLY READ_ONLY FOR 
	SELECT s.value FROM dbo.splitString(@dynProp,'#') s
	WHERE s.value NOT LIKE '%armor%'
	AND s.value NOT LIKE '%const%'
	AND s.value NOT LIKE '%creation%'
	;
	OPEN pieces
	FETCH NEXT FROM pieces INTO @piece
	WHILE (@@FETCH_STATUS = 0)
	BEGIN
	    
		SET @result = @result + @piece + CHAR(13)+CHAR(10) ;
        
		FETCH NEXT FROM pieces INTO @piece
	END
    

	CLOSE pieces; DEALLOCATE pieces;

	RETURN @result;
END
GO