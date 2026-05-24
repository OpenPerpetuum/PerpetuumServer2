using Perpetuum.Items;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Perpetuum.Robots.EquipmentSets
{
    public sealed class EquipmentSetBonusResult
    {
        private static readonly IReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>> _emptyDict =
            new ReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>>(
                new Dictionary<int, IReadOnlyList<ItemPropertyModifier>>());

        public static readonly EquipmentSetBonusResult Empty = new(_emptyDict);

        public EquipmentSetBonusResult(IReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>> modifiersPerSet)
        {
            ModifiersPerSet = modifiersPerSet;
            Modifiers = modifiersPerSet.Values.SelectMany(x => x).ToArray();
        }

        public IReadOnlyDictionary<int, IReadOnlyList<ItemPropertyModifier>> ModifiersPerSet { get; }
        public IReadOnlyList<ItemPropertyModifier> Modifiers { get; }
    }
}
