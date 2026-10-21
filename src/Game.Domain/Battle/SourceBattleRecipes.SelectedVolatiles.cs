using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal static bool HasSelectedVolatileRecipe(string effect) => effect is
        "EFFECT_ATTRACT" or "EFFECT_TAUNT" or "EFFECT_TORMENT" or "EFFECT_SPITE";

    // All four scripts print/spend PP before accuracy, have no Substitute jump,
    // and respect the individual move's Protect flag through accuracycheck.
    internal void ExecuteSelectedVolatile(BattlerSlot actor, BattlerSlot target, int slot,
        PokemonMoveDefinition move, string purpose)
    {
        SpendEarlyPartyPp(actor, target, slot, move);
        void Fail(string reason) => Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, reason));
        if (!SourceGenericAccuracy(actor, target, move, purpose))
        {
            if (move.Effect == "EFFECT_ATTRACT") SourceGenericMiss(actor, target, move);
            else Fail("accuracy");
            return;
        }
        var body = Get(target);
        if (move.Effect == "EFFECT_ATTRACT")
        {
            if (WildBattleEffective.Ability(body) == "ABILITY_OBLIVIOUS")
            {
                RecordAbility(target, "ABILITY_OBLIVIOUS");
                Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, target, move.Id, "ABILITY_OBLIVIOUS")); return;
            }
            // Cmd_tryinfatuating reads the party species/personality, never Transform's view.
            var actorGender = WildBattleTransformationRules.PersistentPokemon(Get(actor)).Gender;
            var targetGender = WildBattleTransformationRules.PersistentPokemon(body).Gender;
            string? reason = actorGender == PokemonGender.Genderless || targetGender == PokemonGender.Genderless ? "genderless" :
                actorGender == targetGender ? "same-gender" : InfatuationSource(target) is not null ? "already-infatuated" : null;
            if (reason is not null) Fail(reason);
            else ChangeInfatuation(target, actor, "attract");
            return;
        }
        if (move.Effect is "EFFECT_TORMENT" or "EFFECT_TAUNT")
        {
            var value = body.CalledVolatile ?? new(); bool torment = move.Effect == "EFFECT_TORMENT";
            if (torment ? value.Tormented : value.TauntTurnsRemaining > 0)
            { Fail(torment ? "already-tormented" : "already-taunted"); return; }
            Set(target, body with { CalledVolatile = torment ? value with { Tormented = true } : value with { TauntTurnsRemaining = 2 } });
            Add(n => new SourceMtMoonVolatileEvent(n, Turn, target, torment ? "torment" : "taunt", 0, torment ? 1 : 2, "move"));
            return;
        }
        int previous = -1;
        if (body.LastUsedMove is { } last && last.Value is not ("pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE"))
            for (int i = 0; i < body.Pokemon.Moves.Length; i++) if (body.Pokemon.Moves[i].Move == last) { previous = i; break; }
        if (previous < 0 || body.Pokemon.Moves[previous].CurrentPp <= 1)
        { Fail("no-reducible-last-move"); return; }
        var learned = body.Pokemon.Moves[previous]; int reduction = (Draw($"{purpose}.spite-pp", 4) & 3) + 2;
        int after = Math.Max(0, learned.CurrentPp - reduction);
        // Set's existing permanent projection protects Transform/Mimic restoration slots.
        Set(target, body with { Pokemon = body.Pokemon with { Moves = body.Pokemon.Moves.SetItem(previous, learned with { CurrentPp = after }) } });
        Add(n => new SourceSpiteEvent(n, Turn, actor, target, learned.Move, previous, learned.CurrentPp, after, reduction));
        if (after == 0) CancellerCancelMultiTurn(target, "spite-empty-pp");
    }

    internal bool TauntPreventsMove(PokemonCatalog catalog, BattlerSlot actor, MoveId move)
    {
        if (Get(actor).CalledVolatile is not { TauntTurnsRemaining: > 0 } || catalog.Moves[move].Power != 0) return false;
        CancellerCancelMultiTurn(actor, "taunt");
        Add(n => new SourceMoveFailedEvent(n, Turn, actor, CancellerFailureTarget(actor), move, "taunted"));
        return true;
    }
}

internal sealed partial class FrameEndTurnContext
{
    internal void TauntResidual(BattlerSlot slot)
    {
        var body = Get(slot);
        if (body.Pokemon.CurrentHp == 0 || body.CalledVolatile is not { TauntTurnsRemaining: > 0 } value) return;
        var next = value with { TauntTurnsRemaining = value.TauntTurnsRemaining - 1 };
        Set(slot, body with { CalledVolatile = next == new WildBattleCalledVolatileState() ? null : next });
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, slot, "taunt", value.TauntTurnsRemaining, next.TauntTurnsRemaining, "countdown"));
    }
}
