using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Domain.Battle;
internal sealed partial class FrameRecipeContext
{
    private readonly Dictionary<BattlerSlot, BattlerSlot> destinyKnockouts = [];
    internal SourceTurnLock? BideLock(BattlerSlot actor) => Frame.Get(actor).Registers.Turn?.Lock is { Kind: WildBattleCalledLockKind.Bide } locked ? locked : null;
    internal void ChangeBideLock(BattlerSlot actor, SourceTurnLock? value, string reason) => ChangeRechargeRampageLock(actor, value, reason);
    internal void StartBide(BattlerSlot actor, PokemonMoveDefinition move) => StartRechargeRampageLock(actor, move, WildBattleCalledLockKind.Bide, 2);
    internal void BideConditionsPassed() => invocation = invocation with {
        CancellerPhase = WildBattleAttackCancellerPhase.ConditionsPassed, Flags = invocation.Flags | WildBattleInvocationFlags.Obeys };
    internal BattlerSlot? BideReleaseTarget(BattlerSlot actor, BattlerSlot stored, PokemonMoveDefinition move)
    {
        if (!Frame.IsSourceAbsent(stored) && !Frame.IsDeferredKoVacant(stored)) return stored;
        var resolved = SourceMoveTargeting.ResolveCalled(Frame, actor, move, "MOVE_TARGET_SELECTED");
        if (!resolved.Succeeded) throw new SourceRecipeUnsupported(resolved.Fault!);
        Frame = Frame with { Rng = resolved.Rng }; Trace.AddRange(resolved.Trace); return resolved.Target;
    }
    internal ulong LateTurnRngCursor => Frame.Rng.Cursor;
    private void BeginLateTurnAction()
    { ClearGrudge(invocation.Actor, "action-attempt"); SetDestinyBond(invocation.Actor, false, "action-attempt"); }
    private void RecordLateTurnDamage(SourceHpChangedEvent hit)
    {
        if (delayedContactDamage) return;
        bool noEffect = fixedDamageNoEffect ?? (hit.Damage == 0 && !HeldDamageEffectiveZero(hit.Target));
        if (!noEffect) StoreBideDamage(hit.Actor, hit.Target, hit.Damage);
        if (hit.Actor.Side != hit.Target.Side && hit.HpBefore > 0 && hit.HpAfter == 0 && Get(hit.Target).DestinyBond)
            destinyKnockouts[hit.Target] = hit.Actor;
    }
    private void ResolveLateTurnFaint(BattlerSlot fainted)
    {
        ApplyGrudgePp(fainted, delayedContactDamage);
        // tryfaintmon stacks Grudge, then Destiny Bond, then the target's faint script.
        if (!destinyKnockouts.Remove(fainted, out var attacker) || Get(attacker).Pokemon.CurrentHp == 0) return;
        TakeDestinyBond(attacker);
    }
}
