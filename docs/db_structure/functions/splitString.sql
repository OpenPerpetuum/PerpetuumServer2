/****** Object:  UserDefinedFunction [dbo].[splitString]    Script Date: 10.05.2026 10:11:10 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO



create FUNCTION [dbo].[splitString]
(
	@input as varchar(max),
    @delimiter as varchar(10) = ","
)
RETURNS 
@result TABLE 
(
	id INT identity(1,1),
    value varchar(max) not null
)
AS
BEGIN
	
    DECLARE @pos AS INT;
    DECLARE @string AS VARCHAR(MAX) = '';

    WHILE LEN(@input) > 0
    BEGIN           
        SELECT @pos = CHARINDEX(@delimiter,@input);

        IF(@pos<=0)
            select @pos = len(@input)

        IF(@pos <> LEN(@input))
            SELECT @string = SUBSTRING(@input, 1, @pos-1);
        ELSE
            SELECT @string = SUBSTRING(@input, 1, @pos);

        INSERT INTO @result SELECT @string

        SELECT @input = SUBSTRING(@input, @pos+len(@delimiter), LEN(@input)-@pos)       
    END

	
	RETURN 
END
GO