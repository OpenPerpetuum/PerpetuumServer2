using System;
using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Modules.Weapons;
using Perpetuum.Units;

namespace Perpetuum.Zones.Effects
{
    /// <summary>
    /// Shared arm/detonate logic for the self-destruct countdown, used by both the
    /// player-piloted SelfDestructModule and HunterDroneAI's SelfDestruct state, so the
    /// detonation behavior exists exactly once.
    /// </summary>
    public static class SelfDestructDetonation
    {
        public static bool IsArmed(Unit owner)
        {
            return owner.EffectHandler.ContainsEffect(EffectType.effect_self_destruct_countdown);
        }

        public static void Arm(
            Unit owner,
            TimeSpan delay,
            double explosionRadius,
            double damageChemical,
            double damageExplosive,
            double damageKinetic,
            double damageThermal)
        {
            if (IsArmed(owner))
            {
                return;
            }

            EffectBuilder effectBuilder = owner.NewEffectBuilder();
            _ = effectBuilder
                .SetType(EffectType.effect_self_destruct_countdown)
                .WithDuration(delay)
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.self_destruct_config_explosion_radius, AggregateFormula.Modifier, explosionRadius))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.self_destruct_config_damage_chemical, AggregateFormula.Modifier, damageChemical))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.self_destruct_config_damage_explosive, AggregateFormula.Modifier, damageExplosive))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.self_destruct_config_damage_kinetic, AggregateFormula.Modifier, damageKinetic))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.self_destruct_config_damage_thermal, AggregateFormula.Modifier, damageThermal));

            owner.ApplyPvPEffect();
            owner.ApplyEffect(effectBuilder);
        }

        public static void Detonate(Unit owner, double explosionRadius, double damageChemical, double damageExplosive, double damageKinetic, double damageThermal)
        {
            if (owner?.Zone == null)
            {
                return;
            }

            var damageBuilder = DamageInfo.Builder.WithAttacker(owner)
                .WithDamage(DamageType.Chemical, damageChemical)
                .WithDamage(DamageType.Explosive, damageExplosive)
                .WithDamage(DamageType.Kinetic, damageKinetic)
                .WithDamage(DamageType.Thermal, damageThermal)
                .WithOptimalRange(2)
                .WithExplosionRadius(explosionRadius);

            owner.Zone.DoAoeDamageAsync(damageBuilder);
            owner.Kill(owner);
        }
    }
}
