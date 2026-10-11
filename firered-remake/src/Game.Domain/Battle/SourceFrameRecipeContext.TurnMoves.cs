using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal override void SourceMultiHitMoveEnd(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    { CompleteScriptTarget(); targetEventStart = Events.Count; }
    internal override void SourceMultiHitFinalMoveEnd() => CompleteScriptTarget();
    internal override bool CancellerCompatibilityLocks(PokemonCatalog catalog, BattlerSlot slot, MoveId move) => DisabledPreventsMove(slot, move) || ImprisonPreventsMove(slot, move);
    internal override void CancellerCancelMultiTurn(BattlerSlot slot, string reason)
    {
        if (RampageLocked(slot)) ChangeRechargeRampageLock(slot, null, reason);
        if (Frame.Get(slot).Registers.Turn?.Lock is { Kind: WildBattleCalledLockKind.Charge } charge)
            ChangeCharge(slot,catalog.Moves[charge.Move],false,reason);
        if (Frame.Get(slot).Registers.Turn?.Lock is { Kind: WildBattleCalledLockKind.Rollout } locked)
            ChangeRollout(slot, catalog.Moves[locked.Move], 0, reason);
    }
    internal override int RolloutRemaining(BattlerSlot slot) => Frame.Get(slot).Registers.Turn?.Lock?.TurnsRemaining ?? 0;
    internal override void ChangeRollout(BattlerSlot slot, PokemonMoveDefinition move, int remaining, string reason)
    {
        var cell = Frame.Get(slot); var turn = cell.Registers.Turn ?? new(WildBattleRules.CalledMoveKernelRulesVersion, null, 0, null);
        var before = turn.Lock;
        // moveTarget retains the selected position; Follow Me only changes this action's recipient.
        var retained = Frame.Queue.FirstOrDefault(a => a.Token?.Owner == action.Actor)?.Target ?? action.Target;
        var locked = remaining == 0 ? null : before is null
            ? new SourceTurnLock(WildBattleCalledLockKind.Rollout, move.Id, invocation.PpSlot,
                Get(slot).Pokemon.Moves[invocation.PpSlot].Move, retained, remaining, 0, null, false)
            : before with { TurnsRemaining = remaining };
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Registers = cell.Registers with {
            Turn = locked is null && turn.SureHitTurnsRemaining == 0 ? null : turn with { Lock = locked } } }) };
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, slot, "called-rollout", before?.TurnsRemaining ?? 0, remaining, reason));
    }
    internal override void RolloutAttackString(BattlerSlot slot)
    {
        if ((invocation.Flags & (WildBattleInvocationFlags.AttackStringPrinted | WildBattleInvocationFlags.NoAttackString)) != 0) return;
        Add(n => new SourceAttackStringEvent(n, Turn, slot, invocation.ChosenMove, move.Id));
        invocation = invocation with { Flags = invocation.Flags | WildBattleInvocationFlags.AttackStringPrinted };
    }
}
