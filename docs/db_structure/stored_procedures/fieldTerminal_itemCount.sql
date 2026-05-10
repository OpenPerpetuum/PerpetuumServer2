/****** Object:  StoredProcedure [dbo].[fieldTerminal_itemCount]    Script Date: 10.05.2026 15:36:54 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[fieldTerminal_itemCount] 
	
	@zoneId INT,
	@publicContainerDefinition int = 166, 
	@owner BIGINT
	
AS
BEGIN
	SET NOCOUNT ON;

	--innen erintetlen 

	SELECT c.eid,COUNT(*) AS amount FROM dbo.getLiveDockingbaseChildren() c 
	JOIN entities i ON i.parent = c.eid and i.owner=@owner AND c.definition=@publicContainerDefinition
	GROUP BY c.eid
	
END
GO