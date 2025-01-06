using Perpetuum.ExportedTypes;

namespace Perpetuum.Modules.ModuleProperties
{
    public class CycleTimeProperty : ModuleProperty
    {
        private readonly ActiveModule _module;

        public CycleTimeProperty(ActiveModule module) : base(module, AggregateField.cycle_time)
        {
            _module = module;
        }

        protected override double CalculateValue()
        {
            var cycleTime = _module.GetPropertyModifier(AggregateField.cycle_time);
            var ammo = _module.GetAmmo();

            ammo?.ModifyCycleTime(ref cycleTime);

            var driller = _module as DrillerModule;

            if (driller != null)
            {
                var miningAmmo = ammo as MiningAmmo;

                if (miningAmmo != null)
                {
                    miningAmmo.miningCycleTimeModifier.Update();

                    var miningCycleTimeMod = miningAmmo.miningCycleTimeModifier.ToPropertyModifier();

                    miningCycleTimeMod.Modify(ref cycleTime);
                }
            }

            ApplyEffectModifiers(ref cycleTime);

            return cycleTime.Value;
        }
    }
}
