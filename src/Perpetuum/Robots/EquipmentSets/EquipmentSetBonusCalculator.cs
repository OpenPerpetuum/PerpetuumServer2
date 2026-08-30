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

            var modifiersPerSet = new Dictionary<int, IReadOnlyList<ItemPropertyModifier>>();

            foreach (KeyValuePair<int, int> entry in countPerSet)
            {
                int setId = entry.Key;
                int count = entry.Value;
                List<ItemPropertyModifier> setModifiers = null;

                foreach (SetBonusThreshold threshold in _repository.GetThresholds(setId))
                {
                    if (threshold.RequiredPieces <= count)
                    {
                        setModifiers ??= new List<ItemPropertyModifier>();
                        setModifiers.Add(ItemPropertyModifier.Create(threshold.Field, threshold.Value));
                    }
                }

                if (setModifiers != null)
                    modifiersPerSet[setId] = setModifiers; // List<T> upcast to IReadOnlyList<T>
            }

            if (modifiersPerSet.Count == 0)
                return EquipmentSetBonusResult.Empty;

            return new EquipmentSetBonusResult(modifiersPerSet);
        }
    }
}
