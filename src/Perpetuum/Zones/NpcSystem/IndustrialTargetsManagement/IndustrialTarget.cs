using Perpetuum.Timers;
using Perpetuum.Units;
using Perpetuum.Zones.NpcSystem.ThreatManaging;
using System;

namespace Perpetuum.Zones.NpcSystem.IndustrialTargetsManagement
{
    public class IndustrialTarget : IComparable<IndustrialTarget>
    {
        private static readonly TimeSpan _threatTimeOut = TimeSpan.FromSeconds(30);

        private double _industrialValue;

        public readonly Position Position;

        public TimeSpan LastIndistrialValueUpdate { get; private set; }

        public double IndustrialValue
        {
            get
            {
                return _industrialValue;
            }
            private set
            {
                if (Math.Abs(_industrialValue - value) <= double.Epsilon)
                {
                    return;
                }

                _industrialValue = value;
                OnIndustrialValueUpdated();
            }
        }

        public bool IsExpired
        {
            get
            {
                return (GlobalTimer.Elapsed - LastIndistrialValueUpdate) >= _threatTimeOut;
            }
        }

        public event Action<IndustrialTarget> Updated;

        public IndustrialTarget(Position position)
        {
            this.Position = position;
            IndustrialValue = 0.0;
        }

        public void AddIndustrialValue(IndustrialValue industrialValue)
        {
            if (industrialValue.value <= 0.0)
            {
                return;
            }

            IndustrialValue += industrialValue.value;
        }

        private void OnIndustrialValueUpdated()
        {
            LastIndistrialValueUpdate = GlobalTimer.Elapsed;
            Updated?.Invoke(this);
        }

        public int CompareTo(IndustrialTarget other)
        {
            if (other._industrialValue < _industrialValue)
            {
                return -1;
            }

            if (other._industrialValue > _industrialValue)
            {
                return 1;
            }

            return 0;
        }
    }
}
