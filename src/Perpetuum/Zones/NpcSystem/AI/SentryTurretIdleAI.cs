using Perpetuum.Zones.NpcSystem.AI.Behaviors;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class SentryTurretIdleAI : StationaryIdleAI
    {
        public SentryTurretIdleAI(SmartCreature smartCreature) : base(smartCreature) { }

        protected override void ToAggressorAI()
        {
            if (this.smartCreature.Behavior.Type == BehaviorType.Passive)
            {
                return;
            }

            this.smartCreature.AI.Push(new SentryTurretCombatAI(this.smartCreature));
        }
    }
}
