namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketNicFlowRow
    {
        public string  Period          { get; init; } = "";
        public long    PlasmaIn        { get; init; }
        public long    RawmatOut       { get; init; }
        public long    NetDelta        => PlasmaIn - RawmatOut;
        public double? PlasmaBudgetPct { get; init; }
        public double? RawmatBudgetPct { get; init; }
    }
}
