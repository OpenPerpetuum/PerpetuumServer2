using Perpetuum.Data;
using Perpetuum.ExportedTypes;
using System.Linq;

namespace Perpetuum.Items
{
    public class DefaultPropertyModifierReader
    {
        private ILookup<int, ItemPropertyModifier> modifiers;

        public void Init()
        {
            modifiers = Db.Query()
                .CommandText("select * from aggregatevalues")
                .Execute()
                .ToLookup(r => r.GetValue<int>("definition"), r =>
                {
                    var field = r.GetValue<AggregateField>("field");
                    var value = r.GetValue<double>("value");

                    return ItemPropertyModifier.Create(field, value);
                });
        }

        public ItemPropertyModifier[] GetByDefinition(int definition)
        {
            return modifiers.GetOrEmpty(definition);
        }
    }
}
