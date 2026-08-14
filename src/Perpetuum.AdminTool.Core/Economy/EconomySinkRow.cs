namespace Perpetuum.AdminTool.Economy
{
    public class EconomySinkRow
    {
        public string Category      { get; init; } = "";
        public long   NicLast30Days { get; init; }
        public double NicPerPlayer  { get; init; }
        public bool   IsTotal       { get; init; }
    }
}
