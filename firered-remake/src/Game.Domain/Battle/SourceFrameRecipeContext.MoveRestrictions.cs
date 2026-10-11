namespace Game.Domain.Battle;
internal sealed partial class FrameRecipeContext
{
    internal override ulong MoveRestrictionRngCursor => Frame.Rng.Cursor;
}
