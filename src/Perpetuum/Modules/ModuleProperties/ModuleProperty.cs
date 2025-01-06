using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using System.Collections.Generic;

namespace Perpetuum.Modules.ModuleProperties
{
    public class ModuleProperty : ItemProperty
    {
        protected readonly Module module;
        private List<AggregateField> _effectModifiers;

        public ModuleProperty(Module module, AggregateField field) : base(field)
        {
            this.module = module;
        }

        public void AddEffectModifier(AggregateField field)
        {
            if (_effectModifiers == null)
            {
                _effectModifiers = new List<AggregateField>();
            }

            _effectModifiers.Add(field);
        }

        protected override double CalculateValue()
        {
            var m = module.GetPropertyModifier(Field);

            ApplyEffectModifiers(ref m);

            return m.Value;
        }

        protected void ApplyEffectModifiers(ref ItemPropertyModifier m)
        {
            if (_effectModifiers == null)
            {
                return;
            }

            foreach (var effectModifier in _effectModifiers)
            {
                module.ParentRobot?.ApplyEffectPropertyModifiers(effectModifier, ref m);
            }
        }

        protected override bool IsRelated(AggregateField field)
        {
            if (_effectModifiers != null && _effectModifiers.Contains(field))
            {
                return true;
            }

            return base.IsRelated(field);
        }
    }
}
