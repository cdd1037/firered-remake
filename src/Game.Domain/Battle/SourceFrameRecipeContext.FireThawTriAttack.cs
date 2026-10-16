namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    // Confusion's datahpupdate changes the real source target to the actor even
    // though the retained requested invocation target remains the opposing slot.
    internal override bool FireHitThawNoEffect => commandNoEffect ||
        Events.Skip(targetEventStart).OfType<SourceConfusionSelfHitEvent>().Any() || Events.Skip(targetEventStart)
        .OfType<SourceMoveFailedEvent>().Any(e => e.Move == move.Id && FireHitFailureSetsNoEffect(e.Reason));
}
