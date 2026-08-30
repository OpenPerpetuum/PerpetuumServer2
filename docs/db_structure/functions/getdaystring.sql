/****** Object:  UserDefinedFunction [dbo].[getdaystring]    Script Date: 10.05.2026 10:38:09 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE FUNCTION [dbo].[getdaystring]
(

	@date datetime
)
RETURNS varchar(20)
AS
BEGIN

	DECLARE @ResultVar varchar(20),@year INT,@month INT, @day INT


	SET @year =DATEPART(year,@date);

	SET @month =datepart(month,@date);
	SET @day =DATEPART(day,@date);

	set @ResultVar = CAST( @year as varchar(4)) + '-' + dbo.padLeft(@month,'0',2) + '-' + dbo.padLeft(@day,'0',2);

	RETURN @ResultVar

END
GO