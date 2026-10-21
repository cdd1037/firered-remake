namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal void MarkSecondaryNoEffect() => commandNoEffect = true;
}
