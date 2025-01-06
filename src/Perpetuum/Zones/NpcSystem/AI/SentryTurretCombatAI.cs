using Perpetuum.Zones.NpcSystem.TargettingStrategies;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class SentryTurretCombatAI : StationaryCombatAI
    {
        public SentryTurretCombatAI(SmartCreature smartCreature) : base(smartCreature) { }

        protected override CombatPrimaryLockSelectionStrategySelector InitSelector()
        {
            return CombatPrimaryLockSelectionStrategySelector.Create()
                .WithStrategy(CombatPrimaryLockSelectionStrategy.HostileOrClosest, 1)
                .Build();
        }
    }
}
