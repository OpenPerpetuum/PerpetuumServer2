/****** Object:  UserDefinedFunction [dbo].[isDefinitionSparkUnlocker]    Script Date: 10.05.2026 10:50:15 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[isDefinitionSparkUnlocker]
(
	@definition int
)
RETURNS int
AS
BEGIN

	DECLARE @sparkActivatorsCF BIGINT;
	SET @sparkActivatorsCF = (SELECT value FROM dbo.categoryFlags WHERE name='cf_package_activator_spark');
	
	DECLARE @defCF BIGINT;
	SET @defCF = (SELECT d.categoryflags FROM dbo.entitydefaults d WHERE d.[definition]=@definition);

	IF (@defCF = @sparkActivatorsCF)
	BEGIN
		RETURN 1;
	END

	RETURN 0;

END
GO