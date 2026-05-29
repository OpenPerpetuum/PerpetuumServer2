namespace Perpetuum.AdminTool.AutoMarket
{
    public class AutoMarketOrderRow
    {
        public string DisplayName { get; init; } = "";
        public string OrderType   { get; init; } = "";
        public double Price       { get; init; }
        public int    Amount      { get; init; }
        public string MarketName  { get; init; } = "";
        public string Category    { get; init; } = "";
    }
}
