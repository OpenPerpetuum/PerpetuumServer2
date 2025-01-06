namespace Perpetuum.Zones.NpcSystem.ThreatManaging
{
    public static class ThreatExtensions
    {
        [CanBeNull]
        public static Hostile GetMostHatedHostile(this IThreatManager manager)
        {
            return manager.Hostiles.Min;
        }
    }
}
