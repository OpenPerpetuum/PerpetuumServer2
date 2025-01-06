using Perpetuum.ExportedTypes;
using System.Collections.Generic;

namespace Perpetuum.Items
{
    public interface IPropertyModifierCollection
    {
        bool TryGetPropertyModifier(AggregateField field, out ItemPropertyModifier modifier);

        ItemPropertyModifier GetPropertyModifier(AggregateField field);

        IEnumerable<ItemPropertyModifier> All { get; }
    }
}
