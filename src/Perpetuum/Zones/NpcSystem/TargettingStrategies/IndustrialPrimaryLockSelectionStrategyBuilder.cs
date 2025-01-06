using Perpetuum.Collections;

namespace Perpetuum.Zones.NpcSystem.TargettingStrategies
{
    public class IndustrialPrimaryLockSelectionStrategyBuilder
    {
        private readonly WeightedCollection<IndustrialPrimaryLockSelectionStrategy> selection;

        public IndustrialPrimaryLockSelectionStrategyBuilder()
        {
            selection = new WeightedCollection<IndustrialPrimaryLockSelectionStrategy>();
        }

        public IndustrialPrimaryLockSelectionStrategyBuilder WithStrategy(IndustrialPrimaryLockSelectionStrategy strategy, int weight = 1)
        {
            selection.Add(strategy, weight);

            return this;
        }

        public IndustrialPrimaryLockSelectionStrategySelector Build()
        {
            return new IndustrialPrimaryLockSelectionStrategySelector(selection);
        }
    }
}
