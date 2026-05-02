using Perpetuum.Modules;
using Perpetuum.Timers;
using Perpetuum.Zones.Locking;
using Perpetuum.Zones.Locking.Locks;
using Perpetuum.Zones.Terrains;
using System;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public sealed class SupportModuleActivator
    {
        private readonly ActiveModule module;
        private readonly IntervalTimer timer = new(TimeSpan.FromSeconds(1), true);

        public SupportModuleActivator(ActiveModule module)
        {
            this.module = module;
        }

        public ActiveModule Module => module;

        public void Update(TimeSpan time, UnitLock supportLock)
        {
            _ = timer.Update(time);

            if (!timer.Passed)
            {
                return;
            }

            timer.Reset();

            if (module.State.Type != ModuleStateType.Idle)
            {
                return;
            }

            if (supportLock == null || supportLock.State != LockState.Locked)
            {
                return;
            }

            // Friendly targets are not in the visibility set (Npc.UpdateUnitVisibility
            // only tracks other-faction NPCs), so we cannot use IUnitVisibility here —
            // query LOS directly via the zone instead.
            LOSResult los = module.ParentRobot.Zone.IsInLineOfSight(module.ParentRobot, supportLock.Target, false);
            if (los != null && los.hit)
            {
                return;
            }

            module.Lock = supportLock;
            module.State.SwitchTo(ModuleStateType.Oneshot);
        }
    }
}
