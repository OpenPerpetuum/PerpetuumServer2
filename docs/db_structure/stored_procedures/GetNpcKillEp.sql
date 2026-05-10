/****** Object:  StoredProcedure [dbo].[GetNpcKillEp]    Script Date: 10.05.2026 16:11:32 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[GetNpcKillEp] 
	@definition int
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @ep INT;
	SET @ep = COALESCE((SELECT TOP 1 r.killep FROM dbo.robottemplaterelation r WHERE r.[definition]= @definition ),0  );

	SELECT @ep;
END
GO