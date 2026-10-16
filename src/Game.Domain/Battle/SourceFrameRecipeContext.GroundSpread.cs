namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal override void GroundSpreadNoEffect() => commandNoEffect = true;
}
