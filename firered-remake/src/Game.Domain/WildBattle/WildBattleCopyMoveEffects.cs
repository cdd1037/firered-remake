using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>A source mimickedMoves bit and its independently stored party slot. Battle PP never writes this slot.</summary>
public sealed record WildBattleMimickedSlot(int Slot, PokemonMoveSlot PersistentSlot);
public sealed record WildBattleCopiedMoves(ImmutableArray<WildBattleMimickedSlot> Slots,
    string RulesVersion = WildBattleRules.CalledMoveKernelRulesVersion);
public sealed record WildBattleMoveCopiedEvent(long Sequence, int Turn, WildBattleSide Actor, WildBattleSide Target,
    MoveId Move, MoveId CopiedMove, int MoveSlot, bool Permanent, PokemonMoveSlot Replacement) : WildBattleEvent(Sequence, Turn);

/// <summary>Source party/live move separation. No new persistent owned-Pokémon schema is needed.</summary>
public static class WildBattleCopyMoveRules
{
    internal static PokemonInstance PersistentPokemon(WildBattleCombatant mon)
    {
        if (mon.CopyMoves is not { } copies) return mon.Pokemon;
        var moves = mon.Pokemon.Moves;
        foreach (var copied in copies.Slots) moves = moves.SetItem(copied.Slot, copied.PersistentSlot);
        return mon.Pokemon with { Moves = moves };
    }

    internal static WildBattleCombatant ApplyPersistentPokemon(WildBattleCombatant mon, PokemonInstance pokemon)
    {
        if (mon.CopyMoves is not { } copies) return mon with { Pokemon = pokemon };
        var moves = pokemon.Moves;
        var slots = copies.Slots.Select(copy => {
            // Cmd_yesnoboxlearnmove: a learned party slot changes, but a mimicked live slot does not.
            var updated = copy with { PersistentSlot = moves[copy.Slot] };
            moves = moves.SetItem(copy.Slot, mon.Pokemon.Moves[copy.Slot]);
            return updated;
        }).ToImmutableArray();
        return mon with { Pokemon = pokemon with { Moves = moves }, CopyMoves = copies with { Slots = slots } };
    }
}

public static partial class WildBattleRules
{
    private static string? ValidateCopiedMoveState(PokemonCatalog catalog, WildBattleState state)
    {
        foreach (var mon in new[] { state.Player, state.Opponent })
        {
            if (mon.CopyMoves is not { } copies) continue;
            if (copies.RulesVersion != CalledMoveKernelRulesVersion) return "copy_move_version";
            if (mon.Transform is not null || copies.Slots.IsDefaultOrEmpty || copies.Slots.Length > 4 ||
                copies.Slots.Any(copy => copy is null || copy.Slot < 0 || copy.Slot >= mon.Pokemon.Moves.Length || copy.PersistentSlot is null) ||
                copies.Slots.Select(copy => copy.Slot).Distinct().Count() != copies.Slots.Length) return "copy_move_slots";
            if (SourcePokemonValidator.ValidateForBattle(catalog, WildBattleCopyMoveRules.PersistentPokemon(mon)) is { } error)
                return "copy_move_persistent_" + error;
        }
        return null;
    }

    internal sealed partial class Execution
    {
        private void CalledCopyMove(PokemonCatalog catalog, WildBattleSide side, PokemonMoveDefinition move)
        {
            var targetSide = activeInvocation!.Target;
            bool permanent = move.Effect == "EFFECT_SKETCH";
            SpendInvocationPp(side, targetSide, move);
            void FailCopy(string reason) => Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, targetSide, move.Id, reason));
            // The scripts test Substitute before either command mutates gChosenMove.
            if (SourceCalledHasSubstitute(targetSide)) { FailCopy("substitute"); return; }
            if (!permanent && !ScalarNoAccuracy(side, targetSide, move))
            { FailCopy(CalledSemiInvulnerable(Get(targetSide)) != WildBattleSemiInvulnerable.None ? "semi-invulnerable" : "protected"); return; }

            activeInvocation = activeInvocation! with { ChosenMove = new("pret:MOVE_UNAVAILABLE") };
            var actor = Get(side); var target = Get(targetSide);
            var copiedId = permanent ? target.SourceMoveHistory?.LastPrintedMove : target.LastUsedMove;
            bool unavailable = copiedId is null || copiedId.Value.Value is "pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE";
            if (actor.Transform is not null) { FailCopy("copy-transformed"); return; }
            if (unavailable || copiedId!.Value.Value is "pret:MOVE_STRUGGLE" or "pret:MOVE_SKETCH" ||
                !permanent && copiedId.Value.Value is "pret:MOVE_METRONOME" or "pret:MOVE_MIMIC")
            { FailCopy("copy-forbidden"); return; }
            if (actor.Pokemon.Moves.Any(slot => slot.Move == copiedId.Value)) { FailCopy("copy-already-known"); return; }
            if (!catalog.Moves.TryGetValue(copiedId.Value, out var definition) || !MetronomeSourceMoveDefinition.Matches(definition))
                throw new UnsupportedAction("copy_move_definition");
            int position = activeInvocation.PpSlot;
            if (position < 0 || position >= actor.Pokemon.Moves.Length) throw new UnsupportedAction("copy_move_slot");
            var original = actor.Pokemon.Moves[position];
            int pp = definition.Pp;
            var replacement = new PokemonMoveSlot(definition.Id, permanent ? pp : Math.Min(5, pp),
                pp + pp * 20 * original.PpUps / 100, original.PpUps);
            var slots = actor.CopyMoves?.Slots ?? [];
            if (!permanent && !slots.Any(copy => copy.Slot == position)) slots = slots.Add(new(position, original));
            actor = actor with { Pokemon = actor.Pokemon with { Moves = actor.Pokemon.Moves.SetItem(position, replacement) } };
            if (permanent)
            {
                // Cmd_copymovepermanently sends REQUEST_MOVES_PP_BATTLE for ALL live slots and ppBonuses.
                // Existing mimickedMoves flags survive; later battle PP still must not update those party slots.
                slots = slots.Select(copy => copy with { PersistentSlot = actor.Pokemon.Moves[copy.Slot] }).ToImmutableArray();
            }
            Set(side, actor with { CopyMoves = slots.IsEmpty ? null : new(slots) });
            Add(n => new WildBattleMoveCopiedEvent(n, State.Turn, side, targetSide, move.Id, definition.Id, position, permanent, replacement));
        }
    }
}
