using Perpetuum.Timers;
using Perpetuum.Units;
using System;

namespace Perpetuum.Zones.NpcSystem.ThreatManaging
{
    public class Hostile : IComparable<Hostile>
    {
        private static readonly TimeSpan _threatTimeOut = TimeSpan.FromSeconds(30);

        private double _threat;

        public readonly Unit Unit;

        public TimeSpan LastThreatUpdate { get; private set; }

        public double Threat
        {
            get
            {
                return _threat;
            }
            private set
            {
                if (Math.Abs(_threat - value) <= double.Epsilon)
                {
                    return;
                }
                    
                _threat = value;
                OnThreatUpdated();
            }
        }

        public bool IsExpired
        {
            get 
            {
                return (GlobalTimer.Elapsed - LastThreatUpdate) >= _threatTimeOut;
            }
        }

        public event Action<Hostile> Updated;

        public Hostile(Unit unit)
        {
            this.Unit = unit;
            Threat = 0.0;
        }

        public void AddThreat(Threat threat)
        {
            if (threat.value <= 0.0)
            {
                return;
            }

            Threat += threat.value;
        }

        private void OnThreatUpdated()
        {
            LastThreatUpdate = GlobalTimer.Elapsed;
            Updated?.Invoke(this);
        }

        public int CompareTo(Hostile other)
        {
            if (other._threat < _threat)
            {
                return -1;
            }

            if (other._threat > _threat)
            {
                return 1;
            }

            return 0;
        }
    }
}
