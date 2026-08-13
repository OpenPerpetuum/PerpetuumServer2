using System;
using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
using Perpetuum.Log;
using Perpetuum.Zones;
using Perpetuum.Zones.Effects;

namespace Perpetuum.Modules
{
    /// <summary>
    /// Kamikaze module: on activation, arms an un-cancellable countdown that detonates an
    /// AoE around the owner and kills the owner's own robot. No target lock is required —
    /// see IMPROVEMENT-043 design spec decisions 1-2.
    /// </summary>
    public class SelfDestructModule : ActiveModule
    {
        private const int BeamVisibility = 600;

        // Guards against ED.Config.ActionDelay being misconfigured to zero (or a negative
        // value) in the DB: SelfDestructDetonation.Arm passes the delay straight to
        // EffectBuilder.WithDuration, which silently skips setting a duration/timer at all
        // for TimeSpan.Zero or negative values — the countdown would be armed but would
        // never expire and never detonate. Falls back to the module's documented default
        // (see the migration's definitionconfig.action_delay = 8000 row) rather than failing
        // activation outright.
        private static readonly TimeSpan FallbackActionDelay = TimeSpan.FromSeconds(8);

        public SelfDestructModule() : base(false)
        {
        }

        public override void AcceptVisitor(IEntityVisitor visitor)
        {
            if (!TryAcceptVisitor(this, visitor))
            {
                base.AcceptVisitor(visitor);
            }
        }

        protected override void OnAction()
        {
            if (ParentRobot?.Zone == null)
            {
                return;
            }

            if (SelfDestructDetonation.IsArmed(ParentRobot))
            {
                return;
            }
            /* We don't need this
            ParentRobot.Zone.CreateBeam(BeamType.timebomb_activation, builder => builder
                .WithPosition(ParentRobot.CurrentPosition)
                .WithVisibility(BeamVisibility)
                .WithDuration(100));
            */
            TimeSpan delay = ED.Config.ActionDelay;

            if (delay <= TimeSpan.Zero)
            {
                Logger.Warning($"SelfDestructModule definition {ED.Definition} has a non-positive ActionDelay ({delay}); falling back to {FallbackActionDelay}.");
                delay = FallbackActionDelay;
            }

            SelfDestructDetonation.Arm(ParentRobot, delay);
        }
    }
}
