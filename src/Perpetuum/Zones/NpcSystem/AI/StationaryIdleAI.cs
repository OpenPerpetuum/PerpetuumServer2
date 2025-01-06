using Perpetuum.Zones.NpcSystem.AI.Behaviors;
using System;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class StationaryIdleAI : BaseAI
    {
        public StationaryIdleAI(SmartCreature smartCreature) : base(smartCreature) { }

        public override void Enter()
        {
            this.smartCreature.StopAllModules();
            this.smartCreature.ResetLocks();
            base.Enter();
        }

        public override void Update(TimeSpan time)
        {
            if (this.smartCreature.ThreatManager.IsThreatened)
            {
                ToAggressorAI();
            }
        }

        protected override void ToHomeAI() { }

        protected override void ToAggressorAI()
        {
            if (this.smartCreature.Behavior.Type == BehaviorType.Passive)
            {
                return;
            }

            this.smartCreature.AI.Push(new StationaryCombatAI(this.smartCreature));
        }
    }
}
