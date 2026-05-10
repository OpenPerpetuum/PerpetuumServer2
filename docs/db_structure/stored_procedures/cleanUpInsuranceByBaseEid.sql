/****** Object:  StoredProcedure [dbo].[cleanUpInsuranceByBaseEid]    Script Date: 10.05.2026 13:32:49 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[cleanUpInsuranceByBaseEid] 

	@baseEid bigint 
	
AS
BEGIN
	
	
	SET NOCOUNT ON;
	DELETE dbo.insurance WHERE eid IN (SELECT eid FROM dbo.treeEids(@baseEid))
	SELECT @@ROWCOUNT
    
	
END
GO