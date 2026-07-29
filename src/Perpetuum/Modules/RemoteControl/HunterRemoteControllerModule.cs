using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
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
            // Deliberately empty: hunter drones have no amplifiable combat stats (their only
            // mechanic is contact self-destruct via SelfDestructDetonation), so no drone_amplification
            // -style effect is needed. Left as an explicit override to document this as a deliberate
            // decision, not an accidental EffectType.undefined effect application.
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
