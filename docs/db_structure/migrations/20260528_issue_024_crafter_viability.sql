BEGIN TRANSACTION;

-- New table: tracks NIC paid for raw material AutoMarket buy order fulfillments
IF OBJECT_ID('dbo.rawmat_purchased', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.rawmat_purchased (
        purchased_on    DATE   NOT NULL,
        item_definition INT    NOT NULL,
        quantity        BIGINT NOT NULL,
        income          FLOAT  NOT NULL,
        CONSTRAINT PK_rawmat_purchased PRIMARY KEY (purchased_on, item_definition)
    );
END;

-- New automarket_config rows for ISSUE-024 crafter viability pricing
MERGE INTO dbo.automarket_config AS target
USING (VALUES
    ('product_sell_margin',     1.2),
    ('raw_mat_sell_multiplier', 1.5),
    ('product_buyback_margin',  0.80),
    ('daily_rawmat_budget_nic', 5000000.0)
) AS src (param_name, param_value)
ON target.param_name = src.param_name
WHEN NOT MATCHED THEN INSERT (param_name, param_value) VALUES (src.param_name, src.param_value);

COMMIT;
