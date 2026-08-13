using System;
using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Units;

namespace Perpetuum.Zones.Effects
{
    /// <summary>
    /// Shared arm/detonate logic for the self-destruct countdown, used by both the
    /// player-piloted SelfDestructModule and HunterDroneAI's SelfDestruct state, so the
    /// detonation behavior exists exactly once.
    ///
    /// Detonation itself deals no bespoke damage: Unit.OnDead always calls DoExplosion(),
    /// which deals AoE damage scaled by the owner's own ArmorMax and current Core ratio
    /// (src/Perpetuum/Units/Unit.cs's GetExplosionDamageBuilder: damage = (sin(coreRatio * pi) + 1)
    /// * (armorMax * 0.1), peaking at coreRatio == 0.5). Arm() drains Core to exactly 50% of
    /// CoreMax and applies a large effect_core_recharge_time_modifier debuff for the countdown's
    /// duration so passive core regen can't drift the ratio away from that peak before detonation.
    /// </summary>
    public static class SelfDestructDetonation
    {
        // AggregateFormula.Modifier normalizes to (value + 1) and multiplies the base value by it
        // (ItemPropertyModifier.cs), so a raw value of 999 means core_recharge_time * 1000 for the
        // countdown's duration -- effectively freezing core regen without depending on any
        // particular robot's own core_recharge_time (which varies a lot: e.g. 720s on an assault
        // drone chassis vs 120s on an attack drone chassis).
        private const double CoreRechargeFreezeModifier = 999;

        public static bool IsArmed(Unit owner)
        {
            return owner.EffectHandler.ContainsEffect(EffectType.effect_self_destruct_countdown);
        }

        public static void Arm(Unit owner, TimeSpan delay)
        {
            if (IsArmed(owner))
            {
                return;
            }

            owner.Core = owner.CoreMax * 0.5;

            EffectBuilder effectBuilder = owner.NewEffectBuilder();
            _ = effectBuilder
                .SetType(EffectType.effect_self_destruct_countdown)
                .WithDuration(delay)
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.effect_core_recharge_time_modifier, AggregateFormula.Modifier, CoreRechargeFreezeModifier));

            owner.ApplyPvPEffect();
            owner.ApplyEffect(effectBuilder);
        }

        public static void Detonate(Unit owner)
        {
            if (owner?.Zone == null)
            {
                return;
            }

            owner.Kill(owner);
        }
    }
}
