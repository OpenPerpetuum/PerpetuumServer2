using System.Linq;

namespace Perpetuum.Items.Extensions
{
    public static class PropertyModifierCollectionExtensions
    {
        public static IPropertyModifierCollection Combine(this IPropertyModifierCollection source, IPropertyModifierCollection target)
        {
            var x = source.All.Select(s => ItemPropertyModifier.Modify(s, target.GetPropertyModifier(s.Field)));

            return new PropertyModifierCollection(x);
        }
    }
}
