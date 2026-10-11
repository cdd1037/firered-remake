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
        private void CalledCopyMove(PokemonCatalog catalog, WildBattleSide side, PokemonMoveDefinition move) =>
            new SinglesRecipeBridge(this).ExecuteCopyState(catalog, SinglesBattleAdapter.Slot(side),
                SinglesBattleAdapter.Slot(activeInvocation!.Target), activeInvocation.PpSlot, move);
    }
}
