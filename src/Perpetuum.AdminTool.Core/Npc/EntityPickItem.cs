namespace Perpetuum.AdminTool.Common
{
    public class EntityPickItem
    {
        public int Definition { get; init; }
        public string Name { get; init; } = "";
        public long CategoryFlags { get; init; }
        public bool Enabled { get; init; }
        public bool Hidden { get; init; }
        public int TierType { get; init; }
        public int TierLevel { get; init; }
        public string Display => $"{Definition} — {Name}";
    }
}
