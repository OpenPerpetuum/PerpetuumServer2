namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketPricingTraceRow
    {
        public string  ResourceName   { get; init; } = "";
        public double  PlasmaAnchor   { get; init; }
        public double  SdRatio        { get; init; }
        public double  RiskMultiplier { get; init; }
        public double  ComputedPrice  { get; init; }
        public double? StoredPrice    { get; init; }
    }
}
