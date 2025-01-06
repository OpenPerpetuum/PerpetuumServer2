using Perpetuum.Collections;
using Perpetuum.Zones.Locking.Locks;

namespace Perpetuum.Zones.NpcSystem.TargettingStrategies
{
    public class CombatPrimaryLockSelectionStrategySelector
    {
        private readonly WeightedCollection<CombatPrimaryLockSelectionStrategy> primaryLockSelectionStrategies;

        public CombatPrimaryLockSelectionStrategySelector(WeightedCollection<CombatPrimaryLockSelectionStrategy> primaryLockSelectionStrategies)
        {
            this.primaryLockSelectionStrategies = primaryLockSelectionStrategies;
        }

        public bool TryUseStrategy(SmartCreature smartCreature, UnitLock[] locks)
        {
            var primaryLockSelectionStrategy = primaryLockSelectionStrategies.GetRandom();

            return CombatStrategies.TryInvokeStrategy(primaryLockSelectionStrategy, smartCreature, locks);
        }

        public static CombatPrimaryLockSelectionStrategyBuilder Create()
        {
            return new CombatPrimaryLockSelectionStrategyBuilder();
        }
    }
}
