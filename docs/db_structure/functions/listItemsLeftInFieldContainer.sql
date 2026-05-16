/****** Object:  UserDefinedFunction [dbo].[listItemsLeftInFieldContainer]    Script Date: 10.05.2026 10:07:31 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[listItemsLeftInFieldContainer] 
(	
	@fieldContainerEid BIGINT 
)
RETURNS TABLE 
AS
RETURN 
(
--47 remove
--46 add


WITH sumlist ([definition],quantity)
AS
(
SELECT [definition], SUM(quantity)* IIF(transactiontype=47,-1,1)  FROM charactertransactions
WHERE containerEID=@fieldContainerEid
AND transactiontype IN (46,47)
 and
definition IS NOT NULL
and
quantity IS NOT NULL

GROUP BY definition,transactiontype
)
SELECT SUM(quantity) AS quantity ,[definition] AS [definition] FROM sumlist group BY definition HAVING SUM(quantity) > 0


)
GO