/****** Object:  View [dbo].[randomView]    Script Date: 10.05.2026 7:25:26 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE VIEW [dbo].[randomView]
AS
SELECT RAND() rndResult
GO