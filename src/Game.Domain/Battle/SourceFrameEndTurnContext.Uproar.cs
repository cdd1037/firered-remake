using Game.Domain.WildBattle;
namespace Game.Domain.Battle;
internal sealed partial class FrameEndTurnContext
{
    internal int UproarRemaining(BattlerSlot actor) => Frame.Get(actor).Registers.Turn?.Lock is { Kind: WildBattleCalledLockKind.Uproar } locked ? locked.TurnsRemaining : 0;
    internal bool UproarUnable(BattlerSlot actor) => Frame.Get(actor).Registers.Turn?.Lock?.UnableToUseMove == true;
    internal void SetUproarRemaining(BattlerSlot actor, int remaining, string reason)
    {
        var cell = Frame.Get(actor); var turn = cell.Registers.Turn!; var locked = turn.Lock!;
        Frame = Frame with { Slots = Frame.Slots.SetItem(actor.Value, cell with { Registers = cell.Registers with {
            Turn = remaining == 0 && turn.SureHitTurnsRemaining == 0 ? null : turn with {
                Lock = remaining == 0 ? null : locked with { TurnsRemaining = remaining } } } }) };
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, "called-uproar", locked.TurnsRemaining, remaining, reason));
    }
}
