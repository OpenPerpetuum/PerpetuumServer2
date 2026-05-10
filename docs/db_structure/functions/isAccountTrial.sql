/****** Object:  UserDefinedFunction [dbo].[isAccountTrial]    Script Date: 10.05.2026 10:47:34 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[isAccountTrial] 
(
	@accountID int
)
RETURNS int
AS
BEGIN
	
	DECLARE @Result INT,@acclevel int

	SET @acclevel = (SELECT acclevel FROM accounts WHERE accountID=@accountID)
	
	IF ((@acclevel & 8388608) > 0)
	BEGIN
		RETURN 1;
	END

	RETURN 0;

END
GO