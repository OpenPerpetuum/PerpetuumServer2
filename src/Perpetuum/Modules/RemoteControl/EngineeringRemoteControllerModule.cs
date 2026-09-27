using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
using Perpetuum.Items;
using Perpetuum.Modules.ModuleProperties;
using Perpetuum.Zones.Effects;
using Perpetuum.Zones.NpcSystem.AI.Behaviors;
using Perpetuum.Zones.RemoteControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Perpetuum.Modules.RemoteControl
{
    public class EngineeringRemoteControllerModule : RemoteControllerModule
    {
        private readonly ModuleProperty turretDamage;
        private readonly ModuleProperty turretCycleTime;
        private readonly ModuleProperty turretLongRange;
        private readonly ModuleProperty turretAccuracy;

        private readonly ModuleProperty turretLockingTime;
        private readonly ModuleProperty turretArmorMax;
        private readonly ModuleProperty turretCoreMax;
        private readonly ModuleProperty turretCoreRechargeTime;
        private readonly ModuleProperty turretReactorRadiation;

        public EngineeringRemoteControllerModule(CategoryFlags ammoCategoryFlags) : base(ammoCategoryFlags)
        {
            turretDamage = new ModuleProperty(this, AggregateField.turret_amplification_damage_modifier);
            AddProperty(turretDamage);

            turretCycleTime = new ModuleProperty(this, AggregateField.turret_amplification_cycle_time_modifier);
            AddProperty(turretCycleTime);

            turretLongRange = new ModuleProperty(this, AggregateField.turret_amplification_long_range_modifier);
            AddProperty(turretLongRange);
            turretAccuracy = new ModuleProperty(this, AggregateField.turret_amplification_accuracy_modifier);
            AddProperty(turretAccuracy);

            turretLockingTime = new ModuleProperty(this, AggregateField.turret_amplification_locking_time_modifier);
            AddProperty(turretLockingTime);

            turretArmorMax = new ModuleProperty(this, AggregateField.turret_amplification_armor_max_modifier);
            AddProperty(turretArmorMax);

            turretCoreMax = new ModuleProperty(this, AggregateField.turret_amplification_core_max_modifier);
            AddProperty(turretCoreMax);

            turretCoreRechargeTime = new ModuleProperty(this, AggregateField.turret_amplification_core_recharge_time_modifier);
            AddProperty(turretCoreRechargeTime);

            turretReactorRadiation = new ModuleProperty(this, AggregateField.turret_amplification_reactor_radiation_modifier);
            AddProperty(turretReactorRadiation);
        }

        public override RemoteControlledCreature CreateAndConfigureRcu(RemoteControlledUnit ammo)
        {
            RemoteControlledCreature remoteControlledCreature = null;
            if (ammo.ED.Options.TurretType == TurretType.Sentry)
            {
                remoteControlledCreature = (SentryTurret)Factory.CreateWithRandomEID(ammo.ED.Options.TurretId);
                remoteControlledCreature.Behavior = Behavior.Create(BehaviorType.RemoteControlledTurret);
                remoteControlledCreature.GuardRange = 5;
            }
            else
            {
                _ = PerpetuumException.Create(ErrorCodes.InvalidAmmoDefinition);
            }

            return remoteControlledCreature;
        }

        protected override void SetupEffect(EffectBuilder effectBuilder)
        {
            double damageModifier = GetPropertyModifier(AggregateField.turret_amplification_damage_modifier).Value;
            double lockingTimeModifier = GetPropertyModifier(AggregateField.turret_amplification_locking_time_modifier).Value;
            double cycleTimeModifier = GetPropertyModifier(AggregateField.turret_amplification_cycle_time_modifier).Value;
            double armorMaxModifier = GetPropertyModifier(AggregateField.turret_amplification_armor_max_modifier).Value;
            double coreMaxModifier = GetPropertyModifier(AggregateField.turret_amplification_core_max_modifier).Value;
            double coreRechargeTimeModifier = GetPropertyModifier(AggregateField.turret_amplification_core_recharge_time_modifier).Value;
            double longRangeModifier = GetPropertyModifier(AggregateField.turret_amplification_long_range_modifier).Value;
            double accuracyModifier = GetPropertyModifier(AggregateField.turret_amplification_accuracy_modifier).Value;
            double reactorRadiationModifier = GetPropertyModifier(AggregateField.turret_amplification_reactor_radiation_modifier).Value;

            _ = effectBuilder
                .SetType(EffectType.drone_amplification)
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.turret_amplification_damage_modifier, AggregateFormula.Modifier, damageModifier))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.turret_amplification_locking_time_modifier, AggregateFormula.Inverse, lockingTimeModifier))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.turret_amplification_cycle_time_modifier, AggregateFormula.Inverse, cycleTimeModifier))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.turret_amplification_armor_max_modifier, AggregateFormula.Modifier, armorMaxModifier))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.turret_amplification_core_max_modifier, AggregateFormula.Modifier, coreMaxModifier))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.turret_amplification_core_recharge_time_modifier, AggregateFormula.Inverse, coreRechargeTimeModifier))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.turret_amplification_long_range_modifier, AggregateFormula.Modifier, longRangeModifier))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.turret_amplification_accuracy_modifier, AggregateFormula.Inverse, accuracyModifier))
                .WithPropertyModifier(new ItemPropertyModifier(AggregateField.turret_amplification_reactor_radiation_modifier, AggregateFormula.Inverse, reactorRadiationModifier));
        }
    }
}
