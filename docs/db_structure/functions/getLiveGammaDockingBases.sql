/****** Object:  UserDefinedFunction [dbo].[getLiveGammaDockingBases]    Script Date: 10.05.2026 10:03:04 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getLiveGammaDockingBases] 
(	
)
RETURNS TABLE 
AS
RETURN 
(
SELECT e.* FROM dbo.zoneuserentities ze 
JOIN dbo.entities e ON e.eid=ze.eid
WHERE e.definition IN(SELECT [definition] FROM dbo.getDefinitionByCFString('cf_pbs_docking_base'))

)
GO