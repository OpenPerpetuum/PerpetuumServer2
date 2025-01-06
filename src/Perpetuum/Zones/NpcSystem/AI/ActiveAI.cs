using Perpetuum.Timers;
using Perpetuum.Units;
using Perpetuum.Zones.Locking.Locks;
using Perpetuum.Zones.Locking;
using Perpetuum.Zones.Terrains;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Perpetuum.Zones.NpcSystem.AI
{
    public class ActiveAI : TurretAI
    {
        private readonly Turret _turret;
        private readonly List<ModuleActivator> _moduleActivators;
        private readonly TimeSpan _minCycleTime;
        private readonly IntervalTimer _primarySelectTimer = new IntervalTimer(0);

        public ActiveAI(Turret turret) : base(turret)
        {
            _turret = turret;

            _moduleActivators = new List<ModuleActivator>();

            _minCycleTime = TimeSpan.FromSeconds(5);

            foreach (var module in _turret.ActiveModules)
            {
                _moduleActivators.Add(new ModuleActivator(module));
                _minCycleTime = _minCycleTime.Min(module.CycleTime);
            }
        }

        public override void Enter()
        {
            foreach (var unitVisibility in _turret.GetVisibleUnits())
            {
                _turret.LockHostile(unitVisibility.Target);
            }

            base.Enter();
        }

        public override void Exit()
        {
            _turret.StopAllModules();
            _turret.ResetLocks();
            base.Exit();
        }

        public override void AttackHostile(Unit unit)
        {
            if (!_turret.IsHostile(unit))
            {
                return;
            }

            _turret.LockHostile(unit);
            base.AttackHostile(unit);
        }

        public override void Update(TimeSpan time)
        {
            if (!SelectPrimaryTarget(time))
            {
                return;
            }

            foreach (var activator in _moduleActivators)
            {
                activator.Update(time);
            }

            base.Update(time);
        }

        private bool SelectPrimaryTarget(TimeSpan time)
        {
            var locks = _turret.GetLocks().Where(l => l.State == LockState.Locked).ToArray();

            if (locks.Length <= 0)
            {
                return false;
            }

            _primarySelectTimer.Update(time);

            if (_primarySelectTimer.Passed)
            {
                _primarySelectTimer.Interval = FastRandom.NextTimeSpan(_minCycleTime);

                var validLocks = new List<UnitLock>();

                foreach (var l in locks)
                {
                    var unitLock = (UnitLock)l;

                    if (unitLock.Primary)
                    {
                        continue;
                    }

                    var visibility = _turret.GetVisibility(unitLock.Target);

                    if (visibility == null)
                    {
                        continue;
                    }

                    var r = visibility.GetLineOfSight(false);

                    if (r != null && r.hit && (r.blockingFlags & BlockingFlags.Plant) == 0)
                    {
                        continue;
                    }

                    validLocks.Add(unitLock);
                }

                if (validLocks.Count > 0)
                {
                    var newPrimary = validLocks.RandomElement();

                    _turret.SetPrimaryLock(newPrimary);

                    return true;
                }
            }

            return locks.Any(l => l.Primary);
        }

        public override void ToActiveAI()
        {
            // nem csinal semmit
        }
    }
}
