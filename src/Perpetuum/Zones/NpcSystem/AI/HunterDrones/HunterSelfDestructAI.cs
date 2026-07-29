using System;
using Perpetuum.Log;
using Perpetuum.PathFinders;
using Perpetuum.Timers;
using Perpetuum.Units;
using Perpetuum.Zones.Effects;
using Perpetuum.Zones.Movements;

namespace Perpetuum.Zones.NpcSystem.AI.HunterDrones
{
    /// <summary>
    /// Un-cancellable detonation countdown state. Per IMPROVEMENT-043 spec decision 12,
    /// the drone actively re-paths toward the target to stay within LeashRange for the
    /// whole countdown; it never transitions back to Approach/Patrol/Retreat once armed,
    /// even if the target dies or leaves the zone (detonation still fires wherever the
    /// drone ends up, matching SelfDestructModule's un-cancellable behavior).
    /// </summary>
    public class HunterSelfDestructAI : HunterDroneAI
    {
        private const double LeashRange = 50;

        // Guards against ED.Config.ActionDelay being misconfigured to zero (or a negative
        // value) in the DB — mirrors SelfDestructModule.OnAction's fallback (see
        // Modules/SelfDestructModule.cs): SelfDestructDetonation.Arm passes the delay
        // straight to EffectBuilder.WithDuration, which silently skips setting a
        // duration/timer at all for TimeSpan.Zero or negative values, so the countdown
        // would be armed but would never expire and never detonate.
        private static readonly TimeSpan FallbackActionDelay = TimeSpan.FromSeconds(8);

        private readonly Unit target;
        private readonly PathFinder pathFinder;
        private readonly IntervalTimer repathTimer = new IntervalTimer(2000);
        private PathMovement movement;

        public HunterSelfDestructAI(SmartCreature smartCreature, Unit target) : base(smartCreature)
        {
            this.target = target;
            pathFinder = new AStarFinder(Heuristic.Manhattan, smartCreature.IsWalkable);
        }

        public override void Enter()
        {
            TimeSpan delay = Drone.ED.Config.ActionDelay;

            if (delay <= TimeSpan.Zero)
            {
                Logger.Warning($"HunterDrone definition {Drone.ED.Definition} has a non-positive ActionDelay ({delay}); falling back to {FallbackActionDelay}.");
                delay = FallbackActionDelay;
            }

            SelfDestructDetonation.Arm(smartCreature, delay);

            RepathToTarget();
            base.Enter();
        }

        public override void Update(TimeSpan time)
        {
            // No IsReceivedRetreatCommand check here — retreat is only honored from
            // HunterApproachAI/HunterPatrolAI. Once armed, retreat commands are
            // intentionally ignored (the countdown is un-cancellable).
            if (target == null || target.States.Dead || !target.InZone)
            {
                movement?.Update(smartCreature, time);
                return;
            }

            if (!smartCreature.CurrentPosition.IsInRangeOf2D(target.CurrentPosition, LeashRange))
            {
                repathTimer.Update(time);
                if (repathTimer.Passed)
                {
                    repathTimer.Reset();
                    RepathToTarget();
                }
            }

            movement?.Update(smartCreature, time);
        }

        private void RepathToTarget()
        {
            if (target == null)
            {
                return;
            }

            pathFinder
                .FindPathAsync(smartCreature.CurrentPosition, target.CurrentPosition)
                .ContinueWith(t =>
                {
                    System.Drawing.Point[] path = t.Result;
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
