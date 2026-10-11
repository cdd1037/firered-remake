using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameEndTurnContext
{
    // Uproar state remains rejected by the four-slot capability boundary.
    internal override bool YawnUproarPreventsSleep(BattlerSlot slot) => false;
    internal override void YawnCancelMultiTurn(BattlerSlot slot)
    {
        var cell = Frame.Get(slot);
        if (cell.Registers.Turn?.Lock is not { Kind: WildBattleCalledLockKind.Charge or WildBattleCalledLockKind.Rollout or WildBattleCalledLockKind.Rampage } locked) return;
        var turn = cell.Registers.Turn;
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Registers = cell.Registers with {
            Turn = turn.SureHitTurnsRemaining == 0 ? null : turn with { Lock = null } } }) };
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, slot,
            locked.Kind == WildBattleCalledLockKind.Charge ? "called-charge" : locked.Kind == WildBattleCalledLockKind.Rampage ? "called-rampage" : "called-rollout", locked.TurnsRemaining, 0, "yawn"));
    }
}

internal static class SourceSleepResidualRules
{
    internal static string? Validate(SourceBattleFrame frame, SourceBattleSlot cell)
    {
        var body = cell.Body!;
        return body.YawnTurnsRemaining is < 0 or > 2 || body.Nightmare && !WildBattleStatus.IsSleeping(body.Status) ||
            !frame.IsSourceAbsent(cell.Slot) && body.Pokemon.CurrentHp == 0 && (body.Nightmare || body.Cursed || body.YawnTurnsRemaining != 0)
            ? "source_sleep_residual_state" : null;
    }
}
