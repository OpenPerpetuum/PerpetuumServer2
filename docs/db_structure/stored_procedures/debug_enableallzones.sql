/****** Object:  StoredProcedure [dbo].[debug_enableallzones]    Script Date: 10.05.2026 13:47:06 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[debug_enableallzones] 
	
AS
BEGIN
	SET NOCOUNT ON;
	

 
UPDATE dbo.zones SET enabled = 1  where [name] LIKE 'zone_%'
	
--pvp arena + quadmap off	
 
UPDATE dbo.zones SET enabled = 0  where id IN (16,12,13,14,15)

--masik jacco off
 
UPDATE dbo.zones SET enabled=0 WHERE id=46

UPDATE dbo.zones SET enabled = 0  where id >=20 AND id <= 44

END

GO