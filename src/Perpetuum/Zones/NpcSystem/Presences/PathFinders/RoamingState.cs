using Perpetuum.Zones.NpcSystem.AI;
using Perpetuum.Zones.NpcSystem.Flocks;
using System;
using System.Linq;

namespace Perpetuum.Zones.NpcSystem.Presences.PathFinders
{
    public class RoamingState : NullRoamingState
    {
        public RoamingState(IRoamingPresence presence) : base(presence)
        {
        }

        public override void Update(TimeSpan time)
        {
            if (IsRunningTask)
            {
                return;
            }

            var members = GetAllMembers();

            if (IsDeadAndExiting(members))
            {
                return;
            }

            if (IsAllNotIdle(members))
            {
                return;
            }

            RunTask(() => FindNextRoamingPosition(), t => { });
        }

        private bool IsAllNotIdle(Npc[] members)
        {
            var idleMembersCount = members.Select(m => m.AI.Current).OfType<IdleAI>().Count();

            return idleMembersCount < members.Length;
        }

        private void FindNextRoamingPosition()
        {
#if DEBUG
            _presence.Log("finding new roaming position. current: " + _presence.CurrentRoamingPosition);
#endif
            var nextRoamingPosition = _presence.PathFinder.FindNextRoamingPosition(_presence);
#if DEBUG
            _presence.Log("next roaming position: " + nextRoamingPosition + " dist:" + _presence.CurrentRoamingPosition.Distance(nextRoamingPosition));
#endif
            _presence.CurrentRoamingPosition = nextRoamingPosition;

            foreach (var npc in _presence.Flocks.GetMembers())
            {
                npc.HomePosition = _presence.CurrentRoamingPosition.ToPosition();
            }
        }
    }
}
