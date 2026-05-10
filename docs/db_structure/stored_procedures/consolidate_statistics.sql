/****** Object:  StoredProcedure [dbo].[consolidate_statistics]    Script Date: 10.05.2026 13:35:20 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[consolidate_statistics]
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	-- This block consolidates and cleans up daily statistics for resources

    -- Step 1: Aggregate from Table B (buffer)
    WITH Aggregated AS (
        SELECT
            gathered_on,
            resource_name,
            SUM(quantity) AS total_quantity
        FROM resources_gathered_daily WITH (READPAST)  -- Skip locked rows (optional)
        GROUP BY gathered_on, resource_name
    )

    -- Step 2: Merge into Table A (summary)
    MERGE INTO resources_gathered AS target
    USING Aggregated AS source
    ON target.gathered_on = source.gathered_on
       AND target.resource_name = source.resource_name
    WHEN MATCHED THEN
        UPDATE SET quantity = target.quantity + source.total_quantity
    WHEN NOT MATCHED THEN
        INSERT (gathered_on, resource_name, quantity)
        VALUES (source.gathered_on, source.resource_name, source.total_quantity);

    -- Step 3: Delete processed rows from Table B
    DELETE FROM resources_gathered_daily;

    -- end of block

    -- This block consolidates and cleans up daily statistics for plasma

    -- Step 1: Aggregate from Table B (buffer)
    WITH Aggregated AS (
        SELECT
            gathered_on,
            plasma_type,
            SUM(quantity) AS total_quantity
        FROM plasma_gathered_daily WITH (READPAST)  -- Skip locked rows (optional)
        GROUP BY gathered_on, plasma_type
    )

    -- Step 2: Merge into Table A (summary)
    MERGE INTO plasma_gathered AS target
    USING Aggregated AS source
    ON target.gathered_on = source.gathered_on
       AND target.plasma_type = source.plasma_type
    WHEN MATCHED THEN
        UPDATE SET quantity = target.quantity + source.total_quantity
    WHEN NOT MATCHED THEN
        INSERT (gathered_on, plasma_type, quantity)
        VALUES (source.gathered_on, source.plasma_type, source.total_quantity);

    -- Step 3: Delete processed rows from Table B
    DELETE FROM plasma_gathered_daily;

    -- end of block
END
GO