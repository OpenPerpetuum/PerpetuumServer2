using SkiaSharp;

namespace Perpetuum.Zones.NpcSystem.Presences.PathFinders
{
    public interface IRoamingPathFinder
    {
        SKPointI FindSpawnPosition(IRoamingPresence presence);
        SKPointI FindNextRoamingPosition(IRoamingPresence presence);
    }
}