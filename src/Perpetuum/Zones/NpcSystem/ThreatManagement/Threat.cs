namespace Perpetuum.Zones.NpcSystem.ThreatManaging
{
    public struct Threat
    {

        public const double WEBBER = 25;
        public const double LOCK_PRIMARY = 2.0;
        public const double LOCK_SECONDARY = 1.0;
        public const double SENSOR_DAMPENER = 25.0;
        public const double BODY_PULL = 1.0;
        public const double SENSOR_BOOSTER = 15;
        public const double REMOTE_SENSOR_BOOSTER = 15;

        public readonly ThreatType type;
        public readonly double value;

        public Threat(ThreatType type, double value)
        {
            this.type = type;
            this.value = value;
        }

        public override string ToString()
        {
            return $"{type} = {value}";
        }

        public static Threat Multiply(Threat threat, double multiplier)
        {
            return new Threat(threat.type, threat.value * multiplier);
        }
    }
}
