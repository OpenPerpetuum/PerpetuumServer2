using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Modules.ModuleProperties;
using Perpetuum.Zones.Effects;

namespace Perpetuum.Modules.EffectModules
{
    public class MineDetectorModule : EffectModule
    {
        private readonly ItemProperty mineDetectionRange;

        public MineDetectorModule()
        {
            mineDetectionRange = new ModuleProperty(this, AggregateField.effect_mine_detection_range_modifier);
            AddProperty(mineDetectionRange);
        }

        public override void AcceptVisitor(IEntityVisitor visitor)
        {
            if (!TryAcceptVisitor(this, visitor))
                base.AcceptVisitor(visitor);
        }

        protected override void SetupEffect(EffectBuilder effectBuilder)
        {
            effectBuilder
                .SetType(EffectType.effect_mine_detector)
                .WithPropertyModifier(mineDetectionRange.ToPropertyModifier());
        }
    }
}
