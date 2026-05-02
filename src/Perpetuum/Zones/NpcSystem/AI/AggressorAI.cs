using System;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class AggressorAI : CombatAI
    {
        public AggressorAI(SmartCreature smartCreature) : base(smartCreature) { }

        public override void Exit()
        {
            this.source?.Cancel();

            base.Exit();
        }

        public override void Update(TimeSpan time)
        {
            if (!smartCreature.IsInHomeRange)
            {
                smartCreature.AI.Push(new HomingAI(smartCreature));

                return;
            }

            if (smartCreature.ShouldFlee())
            {
                smartCreature.AI.Push(new FleeAI(smartCreature));

                return;
            }

            if (smartCreature.HasFriendsNeedingSupport())
            {
                smartCreature.AI.Push(new SupportAI(smartCreature));

                return;
            }

            if (!smartCreature.ThreatManager.IsThreatened)
            {
                ReturnToHomePosition();

                return;
            }

            this.UpdateHostile(time);

            base.Update(time);
        }

        protected override void ToAggressorAI() { }
    }
}
