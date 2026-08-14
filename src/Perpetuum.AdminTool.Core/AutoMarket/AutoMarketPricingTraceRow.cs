namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketPricingTraceRow
    {
        public string  ResourceName   { get; init; } = "";
        public string  DisplayName    { get; set;  } = "";
        public double  PlasmaAnchor   { get; init; }
        public double  SdRatio        { get; init; }
        public double  RiskMultiplier { get; init; }
        public double  ComputedPrice  { get; init; }
        public double? StoredPrice    { get; init; }
        public long    BoughtThisWeek { get; init; }
        public long    EffectiveCap   { get; init; }
    }
}
