USE [perpetuumsa]
GO

---- Upsert raw material AutoMarket purchase record for weekly quantity cap tracking.
---- Called by Market.FulfillSellOrderInstantly for every AutoMarket raw material buy
---- order fulfillment — alongside sp_RecordRawMatPurchased.

CREATE OR ALTER PROCEDURE [dbo].[sp_RecordRawMatWeeklyPurchased]
    @week_start     DATE,
    @definitionname VARCHAR(100),
    @quantity       BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    MERGE dbo.automarket_rawmat_weekly_tracking WITH (HOLDLOCK) AS target
    USING (SELECT @week_start, @definitionname, @quantity)
          AS source(week_start, definitionname, qty_purchased)
    ON  target.week_start     = source.week_start
    AND target.definitionname = source.definitionname
    WHEN MATCHED THEN
        UPDATE SET qty_purchased = target.qty_purchased + source.qty_purchased
    WHEN NOT MATCHED THEN
        INSERT (week_start, definitionname, qty_purchased)
        VALUES (source.week_start, source.definitionname, source.qty_purchased);
END;
GO
