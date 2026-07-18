using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
using Perpetuum.Modules.ModuleProperties;
using Perpetuum.Zones.NpcSystem;
using Perpetuum.Zones.NpcSystem.AI.Behaviors;
using Perpetuum.Zones.RemoteControl;

namespace Perpetuum.Modules
{
    public abstract class HunterRemoteControllerModule : RemoteControllerModule
    {
        private readonly ModuleProperty detectionRange;

        protected HunterRemoteControllerModule(CategoryFlags ammoCategoryFlags) : base(ammoCategoryFlags)
        {
            detectionRange = new ModuleProperty(this, AggregateField.detection_range);
            AddProperty(detectionRange);
        }

        protected abstract Faction? GetTargetFaction();

        public override RemoteControlledCreature CreateAndConfigureRcu(RemoteControlledUnit ammo)
        {
            RemoteControlledCreature remoteControlledCreature;

            if (ammo.ED.Options.TurretType == TurretType.HunterDrone)
            {
                HunterDrone hunterDrone = (HunterDrone)Factory.CreateWithRandomEID(ammo.ED.Options.TurretId);
                hunterDrone.Behavior = Behavior.Create(BehaviorType.RemoteControlledDrone);
                hunterDrone.GuardRange = 5;
                hunterDrone.TargetFaction = GetTargetFaction();
                hunterDrone.DetectionRange = detectionRange.Value;
                remoteControlledCreature = hunterDrone;
            }
            else
            {
                throw PerpetuumException.Create(ErrorCodes.InvalidAmmoDefinition);
            }

            return remoteControlledCreature;
        }
    }

    public class HunterRemoteControllerModulePvE : HunterRemoteControllerModule
    {
        public HunterRemoteControllerModulePvE(CategoryFlags ammoCategoryFlags) : base(ammoCategoryFlags)
        {
        }

        protected override Faction? GetTargetFaction() => Faction.Niani;
    }

    public class HunterRemoteControllerModulePvP : HunterRemoteControllerModule
    {
        public HunterRemoteControllerModulePvP(CategoryFlags ammoCategoryFlags) : base(ammoCategoryFlags)
        {
        }

        protected override Faction? GetTargetFaction() => null;
    }
}
