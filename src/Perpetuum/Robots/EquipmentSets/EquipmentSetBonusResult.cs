using Perpetuum.Items;
using System.Collections.Generic;

namespace Perpetuum.Robots.EquipmentSets
{
    public sealed class EquipmentSetBonusResult
    {
        public static readonly EquipmentSetBonusResult Empty =
            new(Array.Empty<ItemPropertyModifier>(), new HashSet<int>());

        public EquipmentSetBonusResult(
            IReadOnlyList<ItemPropertyModifier> modifiers,
            IReadOnlySet<int> activeSetIds)
        {
            Modifiers = modifiers;
            ActiveSetIds = activeSetIds;
        }

        public IReadOnlyList<ItemPropertyModifier> Modifiers { get; }
        public IReadOnlySet<int> ActiveSetIds { get; }
    }
}
