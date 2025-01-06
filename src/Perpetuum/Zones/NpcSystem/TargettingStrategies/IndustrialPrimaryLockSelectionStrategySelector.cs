using Perpetuum.Collections;
using Perpetuum.Zones.Locking.Locks;

namespace Perpetuum.Zones.NpcSystem.TargettingStrategies
{
    public class IndustrialPrimaryLockSelectionStrategySelector
    {
        private readonly WeightedCollection<IndustrialPrimaryLockSelectionStrategy> primaryLockSelectionStrategies;

        public IndustrialPrimaryLockSelectionStrategySelector(WeightedCollection<IndustrialPrimaryLockSelectionStrategy> primaryLockSelectionStrategies)
        {
            this.primaryLockSelectionStrategies = primaryLockSelectionStrategies;
        }

        public bool TryUseStrategy(SmartCreature smartCreature, TerrainLock[] locks)
        {
            var primaryLockSelectionStrategy = primaryLockSelectionStrategies.GetRandom();

            return IndustrialStrategies.TryInvokeStrategy(primaryLockSelectionStrategy, smartCreature, locks);
        }

        public static IndustrialPrimaryLockSelectionStrategyBuilder Create()
        {
            return new IndustrialPrimaryLockSelectionStrategyBuilder();
        }
    }
}
