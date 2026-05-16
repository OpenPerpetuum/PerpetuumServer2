/****** Object:  UserDefinedFunction [dbo].[aggregateFieldsUsed]    Script Date: 10.05.2026 9:46:31 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[aggregateFieldsUsed]
(	
)
RETURNS TABLE 
AS
RETURN 
(
	SELECT DISTINCT usedFields.field from
	(
	SELECT DISTINCT av.field FROM dbo.aggregatevalues av WHERE av.definition IN (SELECT definition FROM dbo.entitydefaults WHERE enabled=1)
	UNION
	SELECT DISTINCT mp.basefield FROM dbo.modulepropertymodifiers mp
	UNION
	SELECT DISTINCT mp2.modifierfield FROM dbo.modulepropertymodifiers mp2
	UNION
	SELECT DISTINCT em.field FROM dbo.effectdefaultmodifiers em
	UNION
	SELECT DISTINCT ex.targetpropertyID FROM dbo.extensions ex
	) usedFields

)

GO