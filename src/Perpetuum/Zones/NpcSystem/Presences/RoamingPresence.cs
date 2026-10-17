using Perpetuum.StateMachines;
using Perpetuum.Zones.NpcSystem.Flocks;
using Perpetuum.Zones.NpcSystem.Presences.PathFinders;
using SkiaSharp;

namespace Perpetuum.Zones.NpcSystem.Presences
{
    public interface IRoamingPresence
    {
        StackFSM StackFSM { get; }
        Position SpawnOrigin { get; set; }
        SKPointI CurrentRoamingPosition { get; set; }
        IRoamingPathFinder PathFinder { get; set; }
        IPresenceConfiguration Configuration { get; }
        IZone Zone { get; }
        IEnumerable<Flock> Flocks { get; }
        Area Area { get; }
        void Log(string message);
        void OnSpawned();
    }

    public class RoamingPresence : Presence, IRoamingPresence
    {
        public StackFSM StackFSM { get; }
        public Position SpawnOrigin { get; set; }
        public SKPointI CurrentRoamingPosition { get; set; }
        public IRoamingPathFinder PathFinder { get; set; }
        public override Area Area => Configuration.Area;

        public RoamingPresence(IZone zone, IPresenceConfiguration configuration) : base(zone, configuration)
        {
            StackFSM = new StackFSM();
            StackFSM.Push(new SpawnState(this));
        }

        protected override void OnUpdate(TimeSpan time)
        {
            StackFSM.Update(time);
        }

        public void OnSpawned() { }
    }
}