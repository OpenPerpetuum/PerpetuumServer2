using Perpetuum.Log;
using Perpetuum.StateMachines;
using Perpetuum.Zones.Locking.Locks;
using Perpetuum.Zones.NpcSystem.AI.Behaviors;
using Perpetuum.Zones.NpcSystem.AI.IndustrialDrones;
using Perpetuum.Zones.RemoteControl;
using System.Diagnostics;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public abstract class BaseAI : IState
    {
        protected readonly SmartCreature smartCreature;
        private List<ModuleActivator> moduleActivators;

        protected BaseAI(SmartCreature smartCreature)
        {
            this.smartCreature = smartCreature;
        }

        public virtual void Enter()
        {
            moduleActivators = FillModuleActivators();
            WriteLog("enter state = " + GetType().Name);
        }

        public virtual void Exit()
        {
            smartCreature.StopMoving();
            WriteLog("exit state = " + GetType().Name);
        }

        protected virtual List<ModuleActivator> FillModuleActivators()
        {
            return Enumerable.Empty<ModuleActivator>().ToList();
        }

        public virtual void Update(TimeSpan time)
        {
            RunModules(time);
        }

        protected virtual void ToHomeAI()
        {
            smartCreature.AI.Push(new HomingAI(smartCreature));
        }

        protected virtual void ToAggressorAI()
        {
            if (smartCreature.Behavior.Type == BehaviorType.Passive)
            {
                return;
            }

            // Support-fitted NPCs hide instead of brawling — see DECISIONS D1, D2, D10.
            // RemoteControlledCreatures keep their dedicated AI tree (CombatDrones,
            // IndustrialDrones, TurretAI) and are excluded from the redirect.
            if (smartCreature.IsSupportCapable && smartCreature is not RemoteControlledCreature)
            {
                smartCreature.AI.Push(new CoveringAI(smartCreature));

                return;
            }

            smartCreature.AI.Push(new AggressorAI(smartCreature));
        }

        [Conditional("DEBUG")]
        protected void WriteLog(string message)
        {
            Logger.DebugInfo($"SmartCreatureAI: {message}");
        }

        protected virtual void ToEscortIndustrialDroneAI()
        {
            smartCreature.AI.Push(new EscortIndustrialDroneAI(smartCreature));
        }

        protected virtual void ToGatheringIndustrialDroneAI()
        {
            smartCreature.AI.Push(new GatheringIndustrialDroneAI(smartCreature));
        }

        protected TerrainLock GetPrimaryTerrainLock()
        {
            return (smartCreature as RemoteControlledCreature).CommandRobot
                .GetLocks()
                .Where(x => x is TerrainLock && x.Primary)
                .FirstOrDefault() as TerrainLock;
        }

        protected void RunModules(TimeSpan time)
        {
            foreach (ModuleActivator activator in moduleActivators)
            {
                activator.Update(time);
            }
        }
    }
}
