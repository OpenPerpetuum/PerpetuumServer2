namespace Perpetuum.Zones
{
    /// <summary>
    /// Runtime switch for the zone idle throttle (see Zone.Update). Read on the zone tick
    /// thread and written by the zoneIdleThrottleSet admin command on a request thread, so a
    /// plain volatile flag is the whole synchronization a cross-cutting on/off value needs —
    /// no service lifetime, and no Zone constructor change to plumb it.
    /// </summary>
    public static class ZoneIdleThrottling
    {
        private static volatile bool _enabled = true;

        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }
    }
}
