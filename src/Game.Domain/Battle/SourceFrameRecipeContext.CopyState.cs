using Game.Domain.Ids;
namespace Game.Domain.Battle;
internal sealed partial class FrameRecipeContext
{
    internal void CopyChosenUnavailable(bool transform) => invocation = invocation with { ChosenMove = new("pret:MOVE_UNAVAILABLE") };
    internal MoveId? CopyLastPrintedMove(BattlerSlot target) => Frame.Get(target).Registers.History?.LastPrintedMove;
}
