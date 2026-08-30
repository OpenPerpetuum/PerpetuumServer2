CREATE OR ALTER PROCEDURE dbo.usp_RecalculateInsurancePrices AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @fee_pct    FLOAT = (SELECT param_value FROM dbo.insurance_config WHERE param_name = 'fee_pct');
    DECLARE @payout_pct FLOAT = (SELECT param_value FROM dbo.insurance_config WHERE param_name = 'payout_pct');

    IF @fee_pct IS NULL OR @payout_pct IS NULL
    BEGIN
        RAISERROR('insurance_config: fee_pct and payout_pct must both be set.', 16, 1);
        RETURN;
    END

/*
    IF @payout_pct >= @fee_pct
    BEGIN
        RAISERROR('insurance_config: payout_pct must be strictly less than fee_pct to keep insurance a NIC sink.', 16, 1);
        RETURN;
    END
*/

    MERGE dbo.insuranceprices AS t
    USING (
        SELECT
            ed.definition,
            ROUND(vpc.production_cost_nic * @fee_pct,    0) AS fee,
            ROUND(vpc.production_cost_nic * @payout_pct, 0) AS payout
        FROM dbo.v_all_production_costs vpc
        JOIN dbo.entitydefaults ed
            ON ed.definitionname = vpc.product COLLATE DATABASE_DEFAULT
        WHERE ed.definition IN (SELECT definition FROM dbo.insuranceprices)
          AND vpc.production_cost_nic > 0
    ) AS s ON t.definition = s.definition
    WHEN MATCHED THEN
        UPDATE SET t.fee = s.fee, t.payout = s.payout;
END