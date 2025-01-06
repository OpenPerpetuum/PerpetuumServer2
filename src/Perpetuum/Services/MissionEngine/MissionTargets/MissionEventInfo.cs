using Perpetuum.Players;
using Perpetuum.Zones;

namespace Perpetuum.Services.MissionEngine.MissionTargets
{
    public abstract class MissionEventInfo
    {
        public Player Player { get; private set; }

        protected MissionEventInfo(Player player)
        {
            Player = player;
        }

        public abstract MissionTargetType MissionTargetType { get; }
        public abstract Position Position { get; }

        public virtual bool IsDefinitionMatching(IZoneMissionTarget missionTarget)
        {
            return false;
        }
    }
}
