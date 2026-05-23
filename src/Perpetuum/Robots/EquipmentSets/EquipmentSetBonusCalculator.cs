using System.Collections.Generic;
using Perpetuum.Items;

namespace Perpetuum.Robots.EquipmentSets
{
    public class EquipmentSetBonusCalculator : IEquipmentSetBonusCalculator
    {
        private readonly IEquipmentSetRepository _repository;

        public EquipmentSetBonusCalculator(IEquipmentSetRepository repository)
        {
            _repository = repository;
        }

        public EquipmentSetBonusResult Compute(IEnumerable<int> fittedDefinitions)
        {
            if (fittedDefinitions == null)
                return EquipmentSetBonusResult.Empty;

            // Count fitted instances per set_id
            var countPerSet = new Dictionary<int, int>();
            foreach (int def in fittedDefinitions)
            {
                foreach (int setId in _repository.GetSetIdsForDefinition(def))
                {
                    countPerSet.TryGetValue(setId, out int current);
                    countPerSet[setId] = current + 1;
                }
            }

            if (countPerSet.Count == 0)
                return EquipmentSetBonusResult.Empty;

            var modifiers = new List<ItemPropertyModifier>();
            var activeSetIds = new HashSet<int>();

            foreach (KeyValuePair<int, int> entry in countPerSet)
            {
                int setId = entry.Key;
                int count = entry.Value;
                bool anyThresholdMet = false;

                foreach (SetBonusThreshold threshold in _repository.GetThresholds(setId))
                {
                    if (threshold.RequiredPieces <= count)
                    {
                        modifiers.Add(ItemPropertyModifier.Create(threshold.Field, threshold.Value));
                        anyThresholdMet = true;
                    }
                }

                if (anyThresholdMet)
                    activeSetIds.Add(setId);
            }

            if (modifiers.Count == 0)
                return EquipmentSetBonusResult.Empty;

            return new EquipmentSetBonusResult(modifiers, activeSetIds);
        }
    }
}
