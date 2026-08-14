namespace Perpetuum.AdminTool.Economy
{
    public class EconomyCorporationWealthRow
    {
        public int    Rank            { get; init; }
        public string Name            { get; init; } = "";
        public string Tag             { get; init; } = "";
        public int    MemberCount     { get; init; }
        public long   CorpWallet      { get; init; }
        public long   MemberAggregate { get; init; }
        public long   Combined        => CorpWallet + MemberAggregate;
    }
}
