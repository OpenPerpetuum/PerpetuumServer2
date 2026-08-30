namespace Perpetuum.AdminTool.Common
{
    public class ZoneSpawnPickItem
    {
        public int SpawnId { get; init; }
        public string Name { get; init; } = "";
        public string Display => $"{SpawnId} — {Name}";
    }
}
