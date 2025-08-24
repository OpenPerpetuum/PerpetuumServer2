using System.Data;
using System.Runtime.Serialization;

namespace Perpetuum.Services.MissionEngine.MissionTargets
{
    [DataContract]
    [KnownType(typeof(LootItemMissionTarget))]
    [KnownType(typeof(ReachPositionMissionTarget))]
    [KnownType(typeof(KillDefinitionMissionTarget))]
    [KnownType(typeof(ScanMineralMissionTarget))]
    [KnownType(typeof(ScanUnitMissionTarget))]
    [KnownType(typeof(ScanContainerMissionTarget))]
    [KnownType(typeof(DrillMineralMissionTarget))]
    [KnownType(typeof(SubmitItemMissionTarget))]
    [KnownType(typeof(UseSwitchMissionTarget))]
    [KnownType(typeof(FindArtifactMissionTarget))]
    [KnownType(typeof(UseItemsupplyMissionTarget))]
    [KnownType(typeof(HarvestPlantMissionTarget))]
    [KnownType(typeof(SummonNpcEggMissionTarget))]
    [KnownType(typeof(PopNpcMissionTarget))]
    public abstract class MissionTargetRunsOnZone : MissionTarget
    {
        protected MissionTargetRunsOnZone(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTargetRunsOnZone(this);
        }
    }

    [DataContract]
    [KnownType(typeof(PrototypeMissionTarget))]
    [KnownType(typeof(MassproduceMissionTarget))]
    [KnownType(typeof(ResearchMissionTarget))]
    public abstract class MissionTargetProduction : MissionTarget
    {
        protected MissionTargetProduction(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTargetProduction(this);
        }
    }

    [DataContract]
    public class FetchItemMissionTarget : MissionTarget
    {
        public FetchItemMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_fetch_item(this);
        }
    }

    [DataContract]
    public class LootItemMissionTarget : MissionTargetRunsOnZone
    {
        public LootItemMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_loot_item(this);
        }
    }

    [DataContract]
    public class ReachPositionMissionTarget : MissionTargetRunsOnZone
    {
        public ReachPositionMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_reach_position(this);
        }
    }

    [DataContract]
    public class KillDefinitionMissionTarget : MissionTargetRunsOnZone
    {
        public KillDefinitionMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_kill_definition(this);
        }
    }

    [DataContract]
    public class ScanMineralMissionTarget : MissionTargetRunsOnZone
    {
        public ScanMineralMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_scan_mineral(this);
        }
    }

    [DataContract]
    public class ScanUnitMissionTarget : MissionTargetRunsOnZone
    {
        public ScanUnitMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_scan_unit(this);
        }
    }

    [DataContract]
    public class ScanContainerMissionTarget : MissionTargetRunsOnZone
    {
        public ScanContainerMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_scan_container(this);
        }
    }

    [DataContract]
    public class DrillMineralMissionTarget : MissionTargetRunsOnZone
    {
        public DrillMineralMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_drill_mineral(this);
        }
    }

    [DataContract]
    public class SubmitItemMissionTarget : MissionTargetRunsOnZone
    {
        public SubmitItemMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_submit_item(this);
        }
    }

    [DataContract]
    public class UseSwitchMissionTarget : MissionTargetRunsOnZone
    {
        public UseSwitchMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_use_switch(this);
        }
    }

    [DataContract]
    public class FindArtifactMissionTarget : MissionTargetRunsOnZone
    {
        public FindArtifactMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_find_artifact(this);
        }
    }

    [DataContract]
    public class DockInMissionTarget : MissionTarget
    {
        public DockInMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_dock_in(this);
        }
    }

    [DataContract]
    public class UseItemsupplyMissionTarget : MissionTargetRunsOnZone
    {
        public UseItemsupplyMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_use_itemsupply(this);
        }
    }

    [DataContract]
    public class PrototypeMissionTarget : MissionTargetProduction
    {
        public PrototypeMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_prototype(this);
        }
    }

    [DataContract]
    public class MassproduceMissionTarget : MissionTargetProduction
    {
        public MassproduceMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_massproduce(this);
        }
    }

    [DataContract]
    public class ResearchMissionTarget : MissionTargetProduction
    {
        public ResearchMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_research(this);
        }
    }

    [DataContract]
    public class TeleportMissionTarget : MissionTarget
    {
        public TeleportMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_teleport(this);
        }
    }

    [DataContract]
    public class HarvestPlantMissionTarget : MissionTargetRunsOnZone
    {
        public HarvestPlantMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_harvest_plant(this);
        }
    }

    [DataContract]
    public class SummonNpcEggMissionTarget : MissionTargetRunsOnZone
    {
        public SummonNpcEggMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_summon_npc_egg(this);
        }
    }

    [DataContract]
    public class PopNpcMissionTarget : MissionTargetRunsOnZone
    {
        public PopNpcMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_pop_npc(this);
        }
    }

    [DataContract]
    public class SpawnItemMissionTarget : MissionTarget
    {
        public SpawnItemMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_spawn_item(this);
        }
    }

    [DataContract]
    public class LockUnitMissionTarget : MissionTarget
    {

        public LockUnitMissionTarget(IDataRecord record) : base(record) { }

        public override void AcceptVisitor(MissionTargetVisitor visitor)
        {
            visitor.Visit_MissionTarget_lock_unit(this);
        }


    }

}
