using Perpetuum.Units;
using Perpetuum.Zones.RemoteControl;

namespace Perpetuum.Zones.NpcSystem.AI.HunterDrones
{
    public abstract class HunterDroneAI : BaseAI
    {
        protected HunterDroneAI(SmartCreature smartCreature) : base(smartCreature)
        {
        }

        protected HunterDrone Drone => smartCreature as HunterDrone;

        protected void ToHunterPatrolAI()
        {
            smartCreature.AI.Push(new HunterPatrolAI(smartCreature));
        }

        protected void ToHunterApproachAI(Unit target)
        {
            smartCreature.AI.Push(new HunterApproachAI(smartCreature, target));
        }

        protected void ToHunterSelfDestructAI(Unit target)
        {
            smartCreature.AI.Push(new HunterSelfDestructAI(smartCreature, target));
        }

        protected void ToHunterRetreatAI()
        {
            smartCreature.AI.Push(new HunterRetreatAI(smartCreature));
        }
    }
}
