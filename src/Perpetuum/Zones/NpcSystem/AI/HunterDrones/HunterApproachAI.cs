using System;
using Perpetuum.PathFinders;
using Perpetuum.Timers;
using Perpetuum.Units;
using Perpetuum.Zones.Movements;
using SkiaSharp;

namespace Perpetuum.Zones.NpcSystem.AI.HunterDrones
{
    public class HunterApproachAI : HunterDroneAI
    {
        private const double TriggerRange = 2;

        private readonly Unit target;
        private readonly PathFinder pathFinder;
        private readonly IntervalTimer repathTimer = new IntervalTimer(2000);
        private PathMovement movement;

        public HunterApproachAI(SmartCreature smartCreature, Unit target) : base(smartCreature)
        {
            this.target = target;
            pathFinder = new AStarFinder(Heuristic.Manhattan, smartCreature.IsWalkable);
        }

        public override void Enter()
        {
            RepathToTarget();
            base.Enter();
        }

        public override void Update(TimeSpan time)
        {
            if (Drone.IsReceivedRetreatCommand)
            {
                ToHunterRetreatAI();
                return;
            }

            if (target == null || target.States.Dead || !target.InZone)
            {
                ToHunterPatrolAI();
                return;
            }

            if (smartCreature.CurrentPosition.IsInRangeOf2D(target.CurrentPosition, TriggerRange))
            {
                ToHunterSelfDestructAI(target);
                return;
            }

            repathTimer.Update(time);
            if (repathTimer.Passed)
            {
                repathTimer.Reset();
                RepathToTarget();
            }

            movement?.Update(smartCreature, time);
        }

        private void RepathToTarget()
        {
            pathFinder
                .FindPathAsync(smartCreature.CurrentPosition, target.CurrentPosition)
                .ContinueWith(t =>
                {
                    SKPointI[] path = t.Result;
                    if (path == null)
                    {
                        return;
                    }

                    movement = new PathMovement(path);
                    movement.Start(smartCreature);
                });
        }
    }
}
