using Perpetuum.Groups.Gangs;
using Perpetuum.Players;
using Perpetuum.Units;
using Perpetuum.Zones.RemoteControl;
using System.Collections.Generic;
using System.Linq;

namespace Perpetuum.Zones.Effects
{
    /// <summary>
    /// Gang based effect
    /// </summary>
    public class GangEffect : AuraEffect
    {
        protected override void OnTick()
        {
            if (Owner != Source)
            {
                Gang gang = Owner is Player player
                    ? player.Gang
                    : Owner is RemoteControlledCreature remoteControlledCreature &&
                        remoteControlledCreature.CommandRobot is Player ownerPlayer
                        ? ownerPlayer.Gang
                        : null;

                if (gang == null || !gang.IsMember((Player)Source))
                {
                    OnRemoved();

                    return;
                }

            }

            base.OnTick();
        }

        protected override IEnumerable<Unit> GetTargets(IZone zone)
        {
            Player player = (Player)Owner;
            IEnumerable<Player> gangMembers = zone.GetGangMembers(player.Gang);
            IEnumerable<RemoteControlledCreature> alliedTurrets = zone.GetAlliedTurretsByPlayers(new[] { player }.Union(gangMembers));
            IEnumerable<Unit> alliedUnits = gangMembers.Union<Unit>(alliedTurrets);

            return alliedUnits.WithinRange(Owner.CurrentPosition, Radius);
        }

    }
}