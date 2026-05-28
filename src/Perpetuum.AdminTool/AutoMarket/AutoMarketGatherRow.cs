namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketGatherRow
    {
        public string ResourceName { get; init; } = "";
        public long   PveQty       { get; init; }
        public long   PvpQty       { get; init; }
        public long   TotalQty     => PveQty + PvpQty;
        public double PvpPct       => TotalQty > 0 ? PvpQty * 100.0 / TotalQty : 0.0;
    }
}
