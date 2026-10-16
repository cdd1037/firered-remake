using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Battle;
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
        if (mon.CopyMoves is not { } copies) return SourceBattleHeldItems.ReconcileChoice(mon with { Pokemon = pokemon });
        var moves = pokemon.Moves;
        var slots = copies.Slots.Select(copy => {
            // Cmd_yesnoboxlearnmove: a learned party slot changes, but a mimicked live slot does not.
            var updated = copy with { PersistentSlot = moves[copy.Slot] };
            moves = moves.SetItem(copy.Slot, mon.Pokemon.Moves[copy.Slot]);
            return updated;
        }).ToImmutableArray();
        return SourceBattleHeldItems.ReconcileChoice(mon with { Pokemon = pokemon with { Moves = moves }, CopyMoves = copies with { Slots = slots } });
    }
}
