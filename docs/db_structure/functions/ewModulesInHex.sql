/****** Object:  UserDefinedFunction [dbo].[ewModulesInHex]    Script Date: 10.05.2026 9:52:33 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[ewModulesInHex] 
()
RETURNS TABLE 
AS
RETURN 
(


SELECT dbo.ToHex(definition) AS hexn from
(
SELECT * FROM dbo.getDefinitionByCFString('cf_electronic_warfare_equipment')
UNION
SELECT * FROM dbo.getDefinitionByCFString('cf_energy_vampires')
UNION
SELECT * FROM dbo.getDefinitionByCFString('cf_energy_neutralizers')
) AS t




)
GO