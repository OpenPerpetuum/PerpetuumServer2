/****** Object:  StoredProcedure [dbo].[addownerincome]    Script Date: 10.05.2026 7:46:40 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[addownerincome] 
	
	@corpEID bigint, 
	@amount float
AS
BEGIN
	
	
	SET NOCOUNT ON;

	if exists (select amount from ownerincome where corporationeid=@corpEID)
	begin
		update ownerincome set amount=amount+@amount where corporationeid=@corpEID
	end
	else
	begin
		insert ownerincome (corporationeid,amount) values (@corpEID,@amount)
	end
  


	
END


GO