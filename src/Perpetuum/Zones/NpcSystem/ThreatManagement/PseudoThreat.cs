using Perpetuum.Units;
using System;

namespace Perpetuum.Zones.NpcSystem.ThreatManaging
{
    /// <summary>
    /// An expirable record of a player that is aggressing an npc but the npc is
    /// not capable of attacking back (removed from the ThreatManager)
    /// </summary>
    public class PseudoThreat
    {
        private TimeSpan _lastUpdated = TimeSpan.Zero;
        private TimeSpan Expiration = TimeSpan.FromMinutes(1);

        public Unit Unit { get; }

        public bool IsExpired
        {
            get { return _lastUpdated > Expiration; }
        }

        public PseudoThreat(Unit unit)
        {
            Unit = unit;
        }

        public void RefreshThreat()
        {
            _lastUpdated = TimeSpan.Zero;
        }

        public void Update(TimeSpan time)
        {
            _lastUpdated += time;
        }
    }
}
