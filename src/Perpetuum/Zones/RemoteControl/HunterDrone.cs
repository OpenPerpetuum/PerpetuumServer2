using System;
using System.Linq;
using Perpetuum.Players;
using Perpetuum.Robots;
using Perpetuum.Services.Standing;
using Perpetuum.Units;
using Perpetuum.Zones.NpcSystem;

namespace Perpetuum.Zones.RemoteControl
{
    /// <summary>
    /// Autonomous kamikaze drone. Unlike CombatDrone, it does not require the command
    /// robot's target lock — it scans independently for PvE (Niani NPC) or PvP
    /// (hostile-standing player) targets. See IMPROVEMENT-043 design spec.
    /// </summary>
    public class HunterDrone : CombatDrone
    {
        public Faction? TargetFaction { get; set; }

        public double DetectionRange { get; set; }

        // The kamikaze detonation (DoExplosion, triggered via SelfDestructDetonation.Detonate -> Kill)
        // is this drone's only attack — PvE variants must still damage Niani NPCs on Protected (Alpha)
        // islands, unlike an ordinary unit's incidental death explosion, which is deliberately
        // suppressed there. PvP variants only find targets via IsQualifyingTarget/IsHostilePlayer, which
        // already refuses hostile players on Alpha zones without an active PvP flag.
        protected override bool BypassZoneProtectionOnExplosion => true;

        public HunterDrone(IStandingHandler standingHandler) : base(standingHandler)
        {
        }

        public Unit? FindTarget()
        {
            return GetVisibleUnits()
                .Select(v => v.Target)
                .Where(IsQualifyingTarget)
                .OrderBy(u => u.CurrentPosition.TotalDistance3D(CurrentPosition))
                .FirstOrDefault();
        }

        protected override bool IsDetected(Unit target)
        {
            // Deliberately does NOT call base.IsDetected(target): HunterDrone : CombatDrone,
            // and CombatDrone.IsDetected additionally requires IsCommandBotPrimaryLock(target)
            // (CombatDrone.cs) to match the command robot's primary lock — exactly the gate
            // this drone must not have. This re-implements Unit's own default detection
            // formula verbatim (Unit.Visibility.cs, IsDetected: target is Robot robot &&
            // robot.IsLocked(this) => true; otherwise range = 100 / Max(1, StealthStrength)
            // * Max(1, DetectionStrength), checked with IsInRangeOf3D), capped by this
            // drone's own DetectionRange, instead of routing through CombatDrone.
            if (target is Robot robot && robot.IsLocked(this))
            {
                return true;
            }

            double detectionFormulaRange = 100 / Math.Max(1, target.StealthStrength) * Math.Max(1, DetectionStrength);
            double effectiveRange = Math.Min(DetectionRange, detectionFormulaRange);

            return IsInRangeOf3D(target, effectiveRange);
        }

        protected override void UpdateUnitVisibility(Unit target)
        {
            if (target is Npc or Player)
            {
                UpdateVisibility(target);
            }
        }

        private bool IsQualifyingTarget(Unit unit)
        {
            if (TargetFaction != null)
            {
                return unit is Npc npc && npc.ED.Options.Faction == TargetFaction;
            }

            return unit is Player player && IsHostilePlayer(player);
        }
    }
}
