using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal abstract BattlerSlot? MeanLookSource(BattlerSlot target);
    internal abstract void SetMeanLookSource(BattlerSlot target, BattlerSlot actor);
    internal virtual void SetBindingSource(BattlerSlot target, BattlerSlot actor) { }
    internal virtual bool VolatileOwnTempoEnabled => true;
    internal virtual bool BindingStoresMove => true;
    internal virtual bool BindingSandTombName => true;
    internal virtual bool VolatileTargetAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose)
    {
        if (SourceGenericAccuracy(actor, target, move, purpose)) return true;
        SourceGenericMiss(actor, target, move); return false;
    }
    // BattleScript_EffectConfuse and BattleScript_EffectMeanLook preserve their script order.
    // These are selected-position effects. Teeter Dance has a separate source script.
    internal void ExecuteVolatileTarget(BattlerSlot actor, BattlerSlot target, int slot, PokemonMoveDefinition move, string purpose)
    {
        SpendEarlyPartyPp(actor, target, slot, move);
        var body = Get(target);
        void Fail(string reason) => Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, reason));
        if (move.Effect == "EFFECT_CONFUSE")
        {
            if (VolatileOwnTempoEnabled && WildBattleEffective.Ability(body) == "ABILITY_OWN_TEMPO")
            {
                RecordAbility(target, "ABILITY_OWN_TEMPO");
                Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, target, move.Id, "ABILITY_OWN_TEMPO")); return;
            }
            if (SourceCalledSecondaryBlocked(target)) { Fail("substitute"); return; }
            if (body.ConfusionTurnsRemaining != 0) { Fail("already-confused"); return; }
            if (!VolatileTargetAccuracy(actor, target, move, purpose)) return;
            if (SourceCalledSafeguardBlocks(actor, target, move.Id)) return;
            ApplyConfusion(target, purpose, false); return;
        }
        // NO_ACC_CALC does not roll accuracy or apply Normal/Ghost type immunity.
        if (body.DigMoveSlot is not null) { Fail("semi-invulnerable"); return; }
        if (body.Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED")) { SourceGenericMiss(actor, target, move); return; }
        if (MeanLookSource(target) is not null) { Fail("already-trapped"); return; }
        if (SourceCalledSecondaryBlocked(target)) { Fail("substitute"); return; }
        SetMeanLookSource(target, actor);
    }
    // SetMoveEffect(MOVE_EFFECT_WRAP) is outside Shield Dust's <=9 guard.
    internal void ApplySourceBinding(BattlerSlot target, string purpose, MoveId move)
    {
        var body = Get(target);
        if (SourceCalledSecondaryBlocked(target) || body.Pokemon.CurrentHp == 0 || body.BindTurnsRemaining != 0) return;
        int turns = (Draw($"{purpose}.bind-duration", 4) & 3) + 3;
        Set(target, body with { BindTurnsRemaining = turns, BindingMove = BindingStoresMove ? move : null });
        SetBindingSource(target, Invocation?.Actor ?? target.Opposite);
        Add(n => new SourceBindingChangedEvent(n, Turn, target, 0, turns,
            BindingSandTombName && move.Value == "pret:MOVE_SAND_TOMB" ? "sand-tomb" : BindingStoresMove && move.Value == "pret:MOVE_WRAP" ? "wrap" : "bind")
            { Move = BindingStoresMove ? move : null });
    }
}
