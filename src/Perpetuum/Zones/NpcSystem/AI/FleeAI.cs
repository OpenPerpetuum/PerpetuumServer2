using Perpetuum.Modules;
using Perpetuum.Modules.EffectModules;
using Perpetuum.PathFinders;
using Perpetuum.Robots;
using Perpetuum.Zones.Movements;
using Perpetuum.Zones.NpcSystem.ThreatManaging;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class FleeAI : BaseAI
    {
        private const int FallbackRetreatDistance = 60;
        private const int PassableSearchRadius = 10;

        private readonly PathFinder pathFinder;
        private PathMovement movement;
        private bool pathPending;

        private List<ModuleActivator> moduleActivators = new();

        public FleeAI(SmartCreature smartCreature) : base(smartCreature)
        {
            pathFinder = new AStarFinder(Heuristic.Manhattan, smartCreature.IsWalkable);
        }

        public override void Enter()
        {
            moduleActivators.AddRange(smartCreature.ActiveModules
                .OfType<ArmorRepairModule>()
                .Select(m => new ModuleActivator(m)));
            moduleActivators.AddRange(smartCreature.ActiveModules
                .OfType<ShieldGeneratorModule>()
                .Select(m => new ModuleActivator(m)));
            smartCreature.ResetLocks();
            StartRetreatPath();
            base.Enter();
        }

        protected override List<ModuleActivator> FillModuleActivators()
        {
            return moduleActivators;
        }

        public override void Update(TimeSpan time)
        {
            if (ShouldStopFleeing())
            {
                _ = smartCreature.AI.Pop();

                return;
            }

            if (movement != null)
            {
                movement.Update(smartCreature, time);

                if (movement.Arrived)
                {
                    movement = null;
                    StartRetreatPath();
                }
            }

            base.Update(time);
        }

        protected override void ToHomeAI() { }

        protected override void ToAggressorAI() { }

        private bool ShouldStopFleeing()
        {
            if (!smartCreature.ThreatManager.IsThreatened)
            {
                return true;
            }

            if (moduleActivators.Count == 0)
            {
                return true;
            }

            bool armorLow = smartCreature.ArmorPercentage < SmartCreature.FleeArmorRestoreThreshold;
            bool coreLow = smartCreature.HasShieldEffect && smartCreature.CorePercentage < SmartCreature.FleeCoreRestoreThreshold;

            return !armorLow && !coreLow;
        }

        private void StartRetreatPath()
        {
            if (pathPending)
            {
                return;
            }

            pathPending = true;

            Position destination = ComputeRetreatDestination();
            Position start = smartCreature.CurrentPosition;

            _ = pathFinder
                .FindPathAsync(start, destination)
                .ContinueWith(t =>
                {
                    System.Drawing.Point[] path = t.Result;

                    if (path == null)
                    {
                        WriteLog("Flee path not found! (" + start + " => " + destination + ")");

                        AStarFinder fallback = new AStarFinder(Heuristic.Manhattan, (x, y) => true);
                        path = fallback.FindPath(start, destination);
                    }

                    if (path != null)
                    {
                        movement = new PathMovement(path);
                        movement.Start(smartCreature);
                    }

                    pathPending = false;
                });
        }

        private Position ComputeRetreatDestination()
        {
            var hostiles = smartCreature.ThreatManager.Hostiles;
            Position me = smartCreature.CurrentPosition;
            int distance = ComputeRetreatDistance();

            if (!hostiles.Any())
            {
                return smartCreature.HomePosition;
            }

            double cx = 0;
            double cy = 0;
            int count = 0;

            foreach (Hostile h in hostiles)
            {
                Position p = h.Unit.CurrentPosition;
                cx += p.X;
                cy += p.Y;
                count++;
            }

            cx /= count;
            cy /= count;

            double dx = me.X - cx;
            double dy = me.Y - cy;
            double len = Math.Sqrt(dx * dx + dy * dy);

            Position target;
            if (len < 0.001)
            {
                target = me.GetRandomPositionInRange2D(distance, distance);
            }
            else
            {
                target = new Position(me.X + dx / len * distance, me.Y + dy / len * distance);
            }

            Position passable = smartCreature.Zone.FindPassablePointInRadius(target, PassableSearchRadius);
            if (passable == default)
            {
                passable = smartCreature.Zone.FindPassablePointInRadius(me, distance);
            }

            return passable == default ? smartCreature.HomePosition : passable;
        }

        private int ComputeRetreatDistance()
        {
            double maxRange = 0;

            foreach (Hostile h in smartCreature.ThreatManager.Hostiles)
            {
                if (h.Unit is not Robot robot)
                {
                    continue;
                }

                foreach (var module in robot.ActiveModules.Where(m => m.IsRanged))
                {
                    double r = module.OptimalRange + module.Falloff;
                    if (r > maxRange)
                    {
                        maxRange = r;
                    }
                }
            }

            return maxRange > 0 ? (int)Math.Ceiling(maxRange) : FallbackRetreatDistance;
        }
    }
}
