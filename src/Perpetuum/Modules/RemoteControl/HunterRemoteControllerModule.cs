using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Modules.ModuleProperties;
using Perpetuum.Zones.Effects;
using Perpetuum.Zones.NpcSystem;
using Perpetuum.Zones.NpcSystem.AI.Behaviors;
using Perpetuum.Zones.RemoteControl;

namespace Perpetuum.Modules
{
    /// <summary>
    /// Single controller for both hunter drone variants — mirrors IndustrialRemoteControllerModule's
    /// shape (one controller class, one shared ammo categoryflags registration, drone variant picked
    /// per-ammo via TurretType) rather than one controller subclass per variant.
    /// </summary>
    public class HunterRemoteControllerModule : RemoteControllerModule
    {
        private readonly ModuleProperty detectionRange;

        public HunterRemoteControllerModule(CategoryFlags ammoCategoryFlags) : base(ammoCategoryFlags)
        {
            detectionRange = new ModuleProperty(this, AggregateField.detection_range);
            AddProperty(detectionRange);
        }

        protected override void SetupEffect(EffectBuilder effectBuilder)
        {
            double armorMaxModifier = GetPropertyModifier(AggregateField.drone_amplification_armor_max_modifier).Value;
            double coreMaxModifier = GetPropertyModifier(AggregateField.drone_amplification_core_max_modifier).Value;
            double coreRechargeTimeModifier = GetPropertyModifier(AggregateField.drone_amplification_core_recharge_time_modifier).Value;
            double speedMaxModifier = GetPropertyModifier(AggregateField.drone_amplification_speed_max_modifier).Value;
            double reactorRadiationModifier = GetPropertyModifier(AggregateField.drone_amplification_reactor_radiation_modifier).Value;

            _ = effectBuilder
                .SetType(EffectType.drone_amplification)
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.drone_amplification_armor_max_modifier, AggregateFormula.Modifier, armorMaxModifier))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.drone_amplification_core_max_modifier, AggregateFormula.Modifier, coreMaxModifier))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.drone_amplification_core_recharge_time_modifier, AggregateFormula.Inverse, coreRechargeTimeModifier))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.drone_amplification_speed_max_modifier, AggregateFormula.Modifier, speedMaxModifier))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.drone_amplification_reactor_radiation_modifier, AggregateFormula.Inverse, reactorRadiationModifier));
        }

        public override RemoteControlledCreature CreateAndConfigureRcu(RemoteControlledUnit ammo)
        {
            Faction? targetFaction = ammo.ED.Options.TurretType switch
            {
                TurretType.HunterDronePvE => Faction.Niani,
                TurretType.HunterDronePvP => null,
                _ => throw PerpetuumException.Create(ErrorCodes.InvalidAmmoDefinition)
            };

            HunterDrone hunterDrone = (HunterDrone)Factory.CreateWithRandomEID(ammo.ED.Options.TurretId);
            hunterDrone.Behavior = Behavior.Create(BehaviorType.RemoteControlledDrone);
            hunterDrone.GuardRange = 5;
            hunterDrone.TargetFaction = targetFaction;
            hunterDrone.DetectionRange = detectionRange.Value;

            return hunterDrone;
        }
    }
}
