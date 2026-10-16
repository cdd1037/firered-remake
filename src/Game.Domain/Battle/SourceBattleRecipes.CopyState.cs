using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasCopyStateRecipe(string effect) => effect is "EFFECT_TRANSFORM" or "EFFECT_MIMIC" or "EFFECT_SKETCH";
    internal abstract void CopyChosenUnavailable(bool transform);
    internal abstract MoveId? CopyLastPrintedMove(BattlerSlot target);
    internal virtual bool CopyNoAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        CalledSureHit(actor, target) || SemiInvulnerable(target) == WildBattleSemiInvulnerable.None &&
            !(Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED"));

    internal void ExecuteCopyState(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, int slot, PokemonMoveDefinition move)
    {
        SpendEarlyPartyPp(actor, target, slot, move);
        void Fail(string reason) => Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, reason));
        if (move.Effect == "EFFECT_TRANSFORM")
        {
            CopyChosenUnavailable(transform: true);
            Set(actor, Get(actor) with { LastUsedMove = new("pret:MOVE_UNAVAILABLE") });
            if (Get(target).Transform is not null) { Fail("target-transformed"); return; }
            if (SemiInvulnerable(target) != WildBattleSemiInvulnerable.None) { Fail("semi-invulnerable"); return; }
            var donor = Get(target);
            var transformed = WildBattleTransformationRules.Copy(catalog, Get(actor), donor);
            Set(actor, transformed);
            Add(n => new SourceTransformedEvent(n, Turn, actor, target, transformed.Transform!.OriginalPokemon.Species,
                donor.Pokemon.Species, donor.Pokemon.Personality, donor.Pokemon.OriginalTrainerId, transformed.Pokemon));
            return;
        }
        bool permanent = move.Effect == "EFFECT_SKETCH";
        if (SubstitutePresent(target)) { Fail("substitute"); return; }
        if (!permanent && !CopyNoAccuracy(actor, target, move))
        { Fail(SemiInvulnerable(target) != WildBattleSemiInvulnerable.None ? "semi-invulnerable" : "protected"); return; }
        CopyChosenUnavailable(transform: false);
        var body = Get(actor);
        var copiedId = permanent ? CopyLastPrintedMove(target) : Get(target).LastUsedMove;
        bool unavailable = copiedId is null || copiedId.Value.Value is "pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE";
        if (body.Transform is not null) { Fail("copy-transformed"); return; }
        if (unavailable || copiedId!.Value.Value is "pret:MOVE_STRUGGLE" or "pret:MOVE_SKETCH" ||
            !permanent && copiedId.Value.Value is "pret:MOVE_METRONOME" or "pret:MOVE_MIMIC")
        { Fail("copy-forbidden"); return; }
        if (body.Pokemon.Moves.Any(m => m.Move == copiedId.Value)) { Fail("copy-already-known"); return; }
        if (!catalog.Moves.TryGetValue(copiedId.Value, out var definition) || !SourceMoveDefinitionRules.IsValid(definition))
            throw new SourceRecipeUnsupported("copy_move_definition");
        int position = Invocation!.PpSlot;
        if (position < 0 || position >= body.Pokemon.Moves.Length) throw new SourceRecipeUnsupported("copy_move_slot");
        var original = body.Pokemon.Moves[position];
        int pp = definition.Pp;
        var replacement = new PokemonMoveSlot(definition.Id, permanent ? pp : Math.Min(5, pp),
            pp + pp * 20 * original.PpUps / 100, original.PpUps);
        var slots = body.CopyMoves?.Slots ?? [];
        if (!permanent && !slots.Any(copy => copy.Slot == position)) slots = slots.Add(new(position, original));
        body = body with { Pokemon = body.Pokemon with { Moves = body.Pokemon.Moves.SetItem(position, replacement) } };
        // REQUEST_MOVES_PP_BATTLE writes every live slot and ppBonuses, retaining mimickedMoves.
        if (permanent) slots = slots.Select(copy => copy with { PersistentSlot = body.Pokemon.Moves[copy.Slot] }).ToImmutableArray();
        Set(actor, body with { CopyMoves = slots.IsEmpty ? null : new(slots) });
        Add(n => new SourceMoveCopiedEvent(n, Turn, actor, target, move.Id, definition.Id, position, permanent, replacement));
    }
}
