namespace Perpetuum.AdminTool.Economy
{
    public sealed class EconomyNicFlowRow
    {
        public string Category   { get; init; } = "";
        public long   Today      { get; init; }
        public long   Last7Days  { get; init; }
        public long   Last30Days { get; init; }
        public long   AllTime    { get; init; }
        public bool   IsTotal    { get; init; }
    }
}
