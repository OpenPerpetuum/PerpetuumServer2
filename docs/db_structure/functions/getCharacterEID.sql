/****** Object:  UserDefinedFunction [dbo].[getCharacterEID]    Script Date: 10.05.2026 10:34:46 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date, ,>
-- Description:	<Description, ,>
-- =============================================
CREATE FUNCTION [dbo].[getCharacterEID] 
(
	@characterid int
)
RETURNS bigint
AS
BEGIN
	declare @result as bigint

	select @result = rootEID from characters where characterid = @characterid

	return @result
END


GO