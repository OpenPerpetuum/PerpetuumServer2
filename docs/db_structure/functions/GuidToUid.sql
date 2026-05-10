/****** Object:  UserDefinedFunction [dbo].[GuidToUid]    Script Date: 10.05.2026 10:46:30 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date, ,>
-- Description:	<Description, ,>
-- =============================================
CREATE FUNCTION [dbo].[GuidToUid]
(
	@guid as uniqueidentifier
)
RETURNS bigint
AS
BEGIN
	RETURN abs(cast(cast(@guid as varbinary(16)) as bigint))
END

GO