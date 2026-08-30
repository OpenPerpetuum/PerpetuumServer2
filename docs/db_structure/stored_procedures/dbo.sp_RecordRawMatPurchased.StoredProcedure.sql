USE [perpetuumsa]
GO
/****** Object:  StoredProcedure [dbo].[sp_RecordRawMatPurchased]    Script Date: 28.05.2026 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

---- Upsert raw material AutoMarket purchase record for daily NIC budget tracking

CREATE OR ALTER PROCEDURE [dbo].[sp_RecordRawMatPurchased]
    @purchased_on  DATE,
    @item_def      INT,
    @quantity      BIGINT,
    @income        FLOAT
AS
BEGIN
    SET NOCOUNT ON;
    MERGE dbo.rawmat_purchased AS target
    USING (SELECT @purchased_on, @item_def, @quantity, @income)
          AS source(purchased_on, item_definition, quantity, income)
    ON  target.purchased_on    = source.purchased_on
    AND target.item_definition = source.item_definition
    WHEN MATCHED THEN
        UPDATE SET
            quantity = target.quantity + source.quantity,
            income   = target.income   + source.income
    WHEN NOT MATCHED THEN
        INSERT (purchased_on, item_definition, quantity, income)
        VALUES (source.purchased_on, source.item_definition, source.quantity, source.income);
END;
GO
