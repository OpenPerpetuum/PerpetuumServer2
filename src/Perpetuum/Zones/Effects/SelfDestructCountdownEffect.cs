namespace Perpetuum.Zones.Effects
{
    /// <summary>
    /// Countdown for the self-destruct module / hunter drone detonation. Ticks only while
    /// the owner is InZone (via the normal EffectHandler.Update chain), so it naturally
    /// pauses across a teleport's remove-from-zone/re-add gap instead of resetting.
    /// Nothing in the codebase removes effects by this token, so this is inherently
    /// un-cancellable once armed.
    /// </summary>
    public class SelfDestructCountdownEffect : Effect
    {
        protected override void OnRemoved()
        {
            base.OnRemoved();

            SelfDestructDetonation.Detonate(Owner);
        }
    }
}
