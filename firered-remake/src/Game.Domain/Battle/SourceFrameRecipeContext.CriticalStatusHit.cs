namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal override void MarkSecondaryNoEffect() => commandNoEffect = true;
}
