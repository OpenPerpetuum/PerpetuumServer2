using Perpetuum.Zones.RemoteControl;
using System;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class SentryTurretRetreatAI : BaseAI
    {
        public SentryTurretRetreatAI(SmartCreature smartCreature) : base(smartCreature) { }

        public override void Enter()
        {
            smartCreature.StopAllModules();
            smartCreature.ResetLocks();
            base.Enter();
        }

        public override void Update(TimeSpan time)
        {
            SentryTurret turret = smartCreature as SentryTurret;

            if (!turret.IsReceivedRetreatCommand)
            {
                smartCreature.AI.Push(new SentryTurretCombatAI(smartCreature));

                return;
            }

            if (turret.IsInGuardRange)
            {
                turret.Scoop();
            }
        }
    }
}
