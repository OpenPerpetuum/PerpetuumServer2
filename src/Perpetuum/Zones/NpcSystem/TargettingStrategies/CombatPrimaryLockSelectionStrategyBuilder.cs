using Perpetuum.Collections;

namespace Perpetuum.Zones.NpcSystem.TargettingStrategies
{
    public class CombatPrimaryLockSelectionStrategyBuilder
    {
        private readonly WeightedCollection<CombatPrimaryLockSelectionStrategy> selection;

        public CombatPrimaryLockSelectionStrategyBuilder()
        {
            selection = new WeightedCollection<CombatPrimaryLockSelectionStrategy>();
        }

        public CombatPrimaryLockSelectionStrategyBuilder WithStrategy(CombatPrimaryLockSelectionStrategy strategy, int weight = 1)
        {
            selection.Add(strategy, weight);

            return this;
        }

        public CombatPrimaryLockSelectionStrategySelector Build()
        {
            return new CombatPrimaryLockSelectionStrategySelector(selection);
        }
    }
}
