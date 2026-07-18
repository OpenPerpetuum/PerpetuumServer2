using System;
using Perpetuum.Timers;
using Perpetuum.Units;
using Perpetuum.Zones.Movements;

namespace Perpetuum.Zones.NpcSystem.AI.HunterDrones
{
    public class HunterPatrolAI : HunterDroneAI
    {
        private readonly IntervalTimer scanTimer = new IntervalTimer(1000, random: true);
        private RandomMovement movement;

        public HunterPatrolAI(SmartCreature smartCreature) : base(smartCreature)
        {
        }

        public override void Enter()
        {
            smartCreature.StopAllModules();
            smartCreature.ResetLocks();

            movement = new RandomMovement(smartCreature.HomePosition, Drone.HomeRange);
            movement.Start(smartCreature);

            base.Enter();
        }

        public override void Update(TimeSpan time)
        {
            if (Drone.IsReceivedRetreatCommand)
            {
                ToHunterRetreatAI();
                return;
            }

            scanTimer.Update(time);
            if (scanTimer.Passed)
            {
                scanTimer.Reset();

                Unit target = Drone.FindTarget();
                if (target != null)
                {
                    ToHunterApproachAI(target);
                    return;
                }
            }

            movement?.Update(smartCreature, time);
        }
    }
}
