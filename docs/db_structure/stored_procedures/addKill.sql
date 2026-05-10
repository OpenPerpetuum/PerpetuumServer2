/****** Object:  StoredProcedure [dbo].[addKill]    Script Date: 10.05.2026 7:45:58 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[addKill] 
	@characterID int = 0, 
	@killedPlayers int = 0,
	@killedNPCs int = 0,
	@startDate datetime,
	@endDate datetime
	

AS
BEGIN
	SET NOCOUNT ON;

	
	declare @date as datetime
	set @date = convert(char(8),getdate(),112)

	if not exists (select characterid from characterhighscore where characterid=@characterID and date=@date)
	begin
		insert characterhighscore (characterid,playerskilled,npcskilled,date) values
								(@characterID, @killedPlayers, @killedNPCs,@date )

	end 
	else
	begin
	update characterhighscore set
					playerskilled = playerskilled + @killedPlayers,
					npcskilled = npcskilled + @killedNPCs
										
			where characterID=@characterID and date = @date
	end
	
	
	select characterid,sum(playerskilled) as pk
                    from characterhighscore
                    where 
                    characterid=@characterID and 
                    (date between @startDate and @endDate)
                     group by characterid order by pk desc

END









GO