namespace Perpetuum.AdminTool.Economy
{
    public class InsurancePriceRow
    {
        public string ItemName          { get; init; } = "";
        public double ProductionCostNic { get; init; }
        public double Fee               { get; init; }
        public double Payout            { get; init; }
    }
}
