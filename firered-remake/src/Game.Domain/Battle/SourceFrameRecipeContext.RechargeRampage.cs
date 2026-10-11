using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal override bool RampageLocked(BattlerSlot actor) => Frame.Get(actor).Registers.Turn?.Lock?.Kind == WildBattleCalledLockKind.Rampage;
    internal override void StartRechargeRampageLock(BattlerSlot actor, PokemonMoveDefinition move, WildBattleCalledLockKind kind, int remaining)
    {
        var retained = Frame.Queue.FirstOrDefault(a => a.Token?.Owner == action.Actor)?.Target ?? action.Target;
        ChangeRechargeRampageLock(actor, new(kind, move.Id, invocation.PpSlot,
            Get(actor).Pokemon.Moves[invocation.PpSlot].Move, retained, remaining, 0, null, false), "move");
    }
    private void ChangeRechargeRampageLock(BattlerSlot actor, SourceTurnLock? next, string reason)
    {
        var cell = Frame.Get(actor); var turn = cell.Registers.Turn ?? new(WildBattleRules.CalledMoveKernelRulesVersion, null, 0, null);
        var before = turn.Lock;
        if (before == next) return;
        Frame = Frame with { Slots = Frame.Slots.SetItem(actor.Value, cell with { Registers = cell.Registers with {
            Turn = next is null && turn.SureHitTurnsRemaining == 0 ? null : turn with { Lock = next } } }) };
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor,
            "called-" + (next?.Kind ?? before!.Kind).ToString().ToLowerInvariant(), before?.TurnsRemaining ?? 0, next?.TurnsRemaining ?? 0, reason));
    }
    private void RecordRampageInability()
    {
        if (Frame.Get(invocation.Actor).Registers.Turn?.Lock is not { Kind: WildBattleCalledLockKind.Rampage } locked) return;
        if (Events.Any(e => e is SourceEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect } ||
            e is SourceAbilityPreventedEvent { Ability: "ABILITY_LEVITATE" }))
            ChangeRechargeRampageLock(invocation.Actor, locked with { UnableToUseMove = true }, "unable-to-use-move");
    }
}
