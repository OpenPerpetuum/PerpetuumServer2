/****** Object:  StoredProcedure [dbo].[addIP]    Script Date: 10.05.2026 7:45:16 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE PROCEDURE [dbo].[addIP] 
	
	(
	@ip varchar(16),
	@clientID int,
	@banreleaseseconds int
	)
	
AS
	SET NOCOUNT ON
	
	declare @bantime smalldatetime
	
	begin tran
		
		set @bantime = (select top(1) bantime from connectedips where banned=1 and ipaddress=@ip)
		
		if not (@bantime is NULL)
		begin

			--ip was banned once
			if ( datediff (second, @bantime, getdate()) > @banReleaseSeconds)
			begin
				--release the ban
				delete connectedips where banned=1 and ipaddress=@ip
				
			end
			else
			begin
				--still banned: exit
				select -2 --ip was banned
				commit
				return
			end

		end

		--ip is not present in the table: insert
		insert connectedips (ipaddress,clientid) values (@ip,@clientID)

		select 0
	
	commit
	 
	RETURN

GO