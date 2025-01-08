
set nocount on;

declare @trainingZoneId int =45;
declare @trainingEnabled bit;
declare @message varchar(26);
select @trainingEnabled=active from zones where id=45


if (@trainingEnabled=1)
begin
	update zones set active=0 where id=@trainingZoneId;
	set @message = 'training zone is now OFF';
end
else
begin
	update zones set active=1 where id=@trainingZoneId;
	set @message = 'training zone is now ON';
end

select @message as [message]