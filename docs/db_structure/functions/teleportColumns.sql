/****** Object:  UserDefinedFunction [dbo].[teleportColumns]    Script Date: 10.05.2026 10:11:43 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[teleportColumns] 
(	
)
RETURNS TABLE 
AS
RETURN 
(

SELECT DISTINCT tpcEid FROM
(
SELECT sourcecolumn AS tpcEid FROM dbo.teleportdescriptions
UNION
SELECT targetcolumn FROM dbo.teleportdescriptions
) AS kupac
WHERE kupac.tpcEid IS NOT NULL

)
GO