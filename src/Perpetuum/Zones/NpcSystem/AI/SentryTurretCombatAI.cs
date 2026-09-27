using Perpetuum.Zones.Locking.Locks;
using Perpetuum.Zones.NpcSystem.TargettingStrategies;
using Perpetuum.Zones.RemoteControl;
using System;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class SentryTurretCombatAI : StationaryCombatAI
    {
        public SentryTurretCombatAI(SmartCreature smartCreature) : base(smartCreature) { }

        public override void Update(TimeSpan time)
        {
            if ((smartCreature as SentryTurret).IsReceivedRetreatCommand)
            {
                smartCreature.AI.Push(new SentryTurretRetreatAI(smartCreature));

                return;
            }

            base.Update(time);
        }

        protected override CombatPrimaryLockSelectionStrategySelector InitSelector()
        {
            return CombatPrimaryLockSelectionStrategySelector.Create()
                .WithStrategy(CombatPrimaryLockSelectionStrategy.HostileOrClosest, 1)
                .Build();
        }

        // The command robot's primary-locked target, when the turret already holds a valid
        // lock on it, takes priority over the weighted HostileOrClosest selection above.
        protected override bool TryPriorityStrategy(UnitLock[] validLocks)
        {
            return CombatStrategies.TryInvokeStrategy(CombatPrimaryLockSelectionStrategy.PropagatedPrimary, smartCreature, validLocks);
        }
    }
}
