using System;
using Perpetuum.PathFinders;
using Perpetuum.Zones.Movements;
using Perpetuum.Zones.RemoteControl;
using SkiaSharp;

namespace Perpetuum.Zones.NpcSystem.AI.HunterDrones
{
    public class HunterRetreatAI : HunterDroneAI
    {
        private PathMovement movement;
        private readonly PathFinder pathFinder;

        public HunterRetreatAI(SmartCreature smartCreature) : base(smartCreature)
        {
            pathFinder = new AStarFinder(Heuristic.Manhattan, smartCreature.IsWalkable);
        }

        public override void Enter()
        {
            smartCreature.StopAllModules();
            smartCreature.ResetLocks();

            Position randomHome = smartCreature.Zone.FindPassablePointInRadius(smartCreature.HomePosition, (int)Drone.GuardRange);
            if (randomHome == default)
            {
                randomHome = smartCreature.HomePosition;
            }

            pathFinder
                .FindPathAsync(smartCreature.CurrentPosition, randomHome)
                .ContinueWith(t =>
                {
                    SKPointI[] path = t.Result;
                    if (path == null)
                    {
                        path = new AStarFinder(Heuristic.Manhattan, (x, y) => true)
                            .FindPath(smartCreature.CurrentPosition, smartCreature.HomePosition);
                    }

                    movement = new PathMovement(path);
                    movement.Start(smartCreature);
                });

            base.Enter();
        }

        public override void Update(TimeSpan time)
        {
            if (!Drone.IsReceivedRetreatCommand)
            {
                ToHunterPatrolAI();
                return;
            }

            if (movement != null)
            {
                movement.Update(smartCreature, time);

                if (movement.Arrived)
                {
                    if (!Drone.IsInGuardRange)
                    {
                        ToHunterRetreatAI();
                        return;
                    }

                    Drone.Scoop();
                }
            }
        }
    }
}
