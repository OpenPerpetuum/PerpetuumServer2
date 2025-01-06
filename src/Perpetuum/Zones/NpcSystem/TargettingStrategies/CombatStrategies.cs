using Perpetuum.Zones.Locking.Locks;
using Perpetuum.Zones.RemoteControl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Perpetuum.Zones.NpcSystem.TargettingStrategies
{
    public delegate bool CombatTargetSelectionStrategy(SmartCreature smartCreature, UnitLock[] locks);

    public static class CombatStrategies
    {
        public static Dictionary<CombatPrimaryLockSelectionStrategy, CombatTargetSelectionStrategy> All =
            new Dictionary<CombatPrimaryLockSelectionStrategy, CombatTargetSelectionStrategy>()
        {
            { CombatPrimaryLockSelectionStrategy.Random, TargetRandom },
            { CombatPrimaryLockSelectionStrategy.Hostile, TargetMostHated },
            { CombatPrimaryLockSelectionStrategy.Closest, TargetClosest },
            { CombatPrimaryLockSelectionStrategy.OptimalRange, TargetWithinOptimal },
            { CombatPrimaryLockSelectionStrategy.HostileOrClosest, TargetMostHatedOrClosest },
            { CombatPrimaryLockSelectionStrategy.PropagatedPrimary, TargetPropagatedPrimary },
        };

        public static CombatTargetSelectionStrategy GetStrategy(CombatPrimaryLockSelectionStrategy strategyType)
        {
            return All.GetOrDefault(strategyType);
        }

        public static bool TryInvokeStrategy(CombatPrimaryLockSelectionStrategy strategyType, SmartCreature smartCreature, UnitLock[] locks)
        {
            var primaryLockSelectionStrategy = GetStrategy(strategyType);

            if (primaryLockSelectionStrategy == null)
            {
                return false;
            }

            return primaryLockSelectionStrategy(smartCreature, locks);
        }

        private static bool TargetPropagatedPrimary(SmartCreature smartCreature, UnitLock[] locks)
        {
            var propagatedPrimary = locks.FirstOrDefault(x => x.Target == ((smartCreature as RemoteControlledCreature).CommandRobot.GetPrimaryLock() as UnitLock)?.Target);

            if (propagatedPrimary == null)
            {
                smartCreature.ResetLocks();
            }

            return TrySetPrimaryLock(smartCreature, propagatedPrimary);
        }

        private static bool TargetMostHated(SmartCreature smartCreature, UnitLock[] locks)
        {
            var hostiles = smartCreature.ThreatManager.Hostiles;
            var hostileLocks = locks.Where(u => hostiles.Any(h => h.Unit.Eid == u.Target.Eid));
            var mostHostileLock = hostileLocks
                .OrderByDescending(u => hostiles.Where(h => h.Unit.Eid == u.Target.Eid).FirstOrDefault()?.Threat ?? 0)
                .FirstOrDefault();

            return TrySetPrimaryLock(smartCreature, mostHostileLock);
        }

        private static bool TargetWithinOptimal(SmartCreature smartCreature, UnitLock[] locks)
        {
            return TrySetPrimaryLock(
                smartCreature,
                locks
                    .Where(k => k.Target.GetDistance(smartCreature) < smartCreature.BestActionRange)
                    .RandomElement());
        }

        private static bool TargetClosest(SmartCreature smartCreature, UnitLock[] locks)
        {
            return TrySetPrimaryLock(smartCreature, locks.OrderBy(u => u.Target.GetDistance(smartCreature)).First());
        }

        private static bool TargetRandom(SmartCreature smartCreature, UnitLock[] locks)
        {
            return TrySetPrimaryLock(smartCreature, locks.RandomElement());
        }

        private static bool TargetMostHatedOrClosest(SmartCreature smartCreature, UnitLock[] locks)
        {
            return TargetMostHated(smartCreature, locks) || TargetClosest(smartCreature, locks);
        }

        private static bool TrySetPrimaryLock(SmartCreature smartCreature, Lock targetLock)
        {
            if (targetLock == null)
            {
                return false;
            }

            smartCreature.SetPrimaryLock(targetLock);

            return true;
        }
    }
}
