using Perpetuum.ExportedTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Perpetuum.Items
{
    public class PropertyModifierCollection : IPropertyModifierCollection
    {
        public static readonly PropertyModifierCollection Empty = new PropertyModifierCollection();
        private readonly Dictionary<AggregateField, ItemPropertyModifier> modifiers = new Dictionary<AggregateField, ItemPropertyModifier>();

        private PropertyModifierCollection()
        {

        }

        public PropertyModifierCollection(IEnumerable<ItemPropertyModifier> modifiers)
        {
            this.modifiers = modifiers.ToDictionary(m => m.Field);
        }

        public bool TryGetPropertyModifier(AggregateField field, out ItemPropertyModifier modifier)
        {
            return modifiers.TryGetValue(field, out modifier);
        }

        public ItemPropertyModifier GetPropertyModifier(AggregateField field)
        {
            if (TryGetPropertyModifier(field, out ItemPropertyModifier m))
            {
                return m;
            }

            return ItemPropertyModifier.Create(field);
        }

        public IEnumerable<ItemPropertyModifier> All => modifiers.Values;
    }
}
