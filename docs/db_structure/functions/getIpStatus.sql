/****** Object:  UserDefinedFunction [dbo].[getIpStatus]    Script Date: 10.05.2026 10:40:25 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE FUNCTION [dbo].[getIpStatus] 
(
	
	@value int
)
RETURNS varchar(50)
AS
BEGIN

	-- Declare the return variable here
	DECLARE @Result varchar(50)

	set @Result  = (select 'tofi' = 
	case
		when @value = -1 then 'Unknown'
		when @value = 0 then 'Success'
		when @value = 11002 then 'DestinationNetworkUnreachable'
		when @value = 11003 then 'DestinationHostUnreachable'
		when @value = 11004 then 'DestinationProtocolUnreachable'
		when @value = 11005 then 'DestinationPortUnreachable'
		when @value = 11006 then 'NoResources'
		when @value = 11007 then 'BadOption'
		when @value = 11008 then 'HardwareError'
		when @value = 11009 then 'PacketTooBig'
		when @value = 11010 then 'TimedOut'
		when @value = 11012 then 'BadRoute'
		when @value = 11013 then 'TtlExpired'
		when @value = 11014 then 'TtlReassemblyTimeExceeded'
		when @value = 11015 then 'ParameterProblem'
		when @value = 11016 then 'SourceQuench'
		when @value = 11018 then 'BadDestination'
		when @value = 11040 then 'DestinationUnreachable'
		when @value = 11041 then 'TimeExceeded'
		when @value = 11042 then 'BadHeader'
		when @value = 11043 then 'UnrecognizedNextHeader'
		when @value = 11044 then 'IcmpError'
		when @value = 11045 then 'DestinationScopeMismatch'
		else 'wtf'
	end)

	-- Return the result of the function
	RETURN @Result

END
GO