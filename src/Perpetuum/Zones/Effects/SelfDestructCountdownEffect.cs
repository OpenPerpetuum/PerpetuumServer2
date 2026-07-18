using System.Linq;
using Perpetuum.ExportedTypes;

namespace Perpetuum.Zones.Effects
{
    /// <summary>
    /// Countdown for the self-destruct module / hunter drone detonation. Ticks only while
    /// the owner is InZone (via the normal EffectHandler.Update chain), so it naturally
    /// pauses across a teleport's remove-from-zone/re-add gap instead of resetting.
    /// Nothing in the codebase removes effects by this token, so this is inherently
    /// un-cancellable once armed.
    /// </summary>
    public class SelfDestructCountdownEffect : Effect
    {
        protected override void OnRemoved()
        {
            base.OnRemoved();

            double explosionRadius = GetConfigValue(AggregateField.self_destruct_config_explosion_radius);
            double damageChemical = GetConfigValue(AggregateField.self_destruct_config_damage_chemical);
            double damageExplosive = GetConfigValue(AggregateField.self_destruct_config_damage_explosive);
            double damageKinetic = GetConfigValue(AggregateField.self_destruct_config_damage_kinetic);
            double damageThermal = GetConfigValue(AggregateField.self_destruct_config_damage_thermal);

            SelfDestructDetonation.Detonate(Owner, explosionRadius, damageChemical, damageExplosive, damageKinetic, damageThermal);
        }

        private double GetConfigValue(AggregateField field)
        {
            // ItemPropertyModifier is a struct, so FirstOrDefault's "not found" result is
            // default(ItemPropertyModifier), whose Value is already 0.0 — no null-coalescing needed.
            return PropertyModifiers.FirstOrDefault(m => m.Field == field).Value;
        }
    }
}
