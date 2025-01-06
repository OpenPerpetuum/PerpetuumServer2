using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Modules.Weapons;

namespace Perpetuum.Modules.ModuleProperties
{
    public class FalloffProperty : ModuleProperty
    {
        public FalloffProperty(ActiveModule module) : base(module, AggregateField.falloff)
        {
        }

        protected override double CalculateValue()
        {
            var falloff = ItemPropertyModifier.Create(AggregateField.falloff);
            var ammo = ((ActiveModule)module).GetAmmo();

            if (module is MissileWeaponModule m)
            {
                if (ammo != null)
                {
                    falloff = ammo.FalloffRangePropertyModifier;

                    var missileRangeMod = m.MissileFalloffModifier.ToPropertyModifier();

                    missileRangeMod.Modify(ref falloff);
                    module.ApplyRobotPropertyModifiers(ref falloff);
                }
            }
            else
            {
                falloff = module.GetPropertyModifier(AggregateField.falloff);
                ammo?.ModifyFalloff(ref falloff);
            }

            ApplyEffectModifiers(ref falloff);

            return falloff.Value;
        }
    }
}
