/****** Object:  StoredProcedure [dbo].[cleanUpMarket]    Script Date: 10.05.2026 13:33:25 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[cleanUpMarket]

AS
BEGIN
	SET NOCOUNT ON;

    delete marketitems where itemdefinition in (select definition from entitydefaults where enabled=0 or hidden=1)

	select @@ROWCOUNT
	END
GO