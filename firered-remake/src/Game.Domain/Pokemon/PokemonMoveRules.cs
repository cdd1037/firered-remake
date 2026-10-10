using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

public static class PokemonMoveRules
{
    /// <summary>Ordinary learning cannot forget HM moves (IsHMMove2 in the original learn-move menu).</summary>
    public static bool IsHiddenMachineMove(MoveId move) => move.Value is "pret:MOVE_CUT" or "pret:MOVE_FLY" or "pret:MOVE_SURF" or "pret:MOVE_STRENGTH" or "pret:MOVE_FLASH" or "pret:MOVE_ROCK_SMASH" or "pret:MOVE_WATERFALL" or "pret:MOVE_DIVE";

    /// <summary>The gift profile keeps every source move as metadata while deferring learning of unimplemented battle effects.</summary>
    public static bool IsLearningSupported(PokemonCatalog catalog, MoveId move) =>
        catalog.Species.Count == 107 ? Game.Domain.WildBattle.WildBattleRules.IsMoveSupported(move, Game.Domain.WildBattle.WildBattleRules.FuchsiaRulesVersion) : catalog.Species.Count != 103 || Game.Domain.WildBattle.WildBattleRules.IsMoveSupported(move, Game.Domain.WildBattle.WildBattleRules.SnorlaxRulesVersion);

    /// <summary>CreateBoxMon learns in source order. Only initial creation automatically forgets the oldest move.</summary>
    public static ImmutableArray<PokemonMoveSlot> InitialMoves(PokemonCatalog catalog, PokemonSpeciesDefinition species, int level)
    {
        PokemonFactory.ValidateCatalog(catalog);
        if (level is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(level));
        ArgumentNullException.ThrowIfNull(species);
        if (!catalog.Species.TryGetValue(species.Id, out var registered) || registered != species)
            throw new ArgumentException("Species must be the catalog definition.");
        ImmutableArray<PokemonMoveSlot> moves = [];
        foreach (var entry in species.LevelUpMoves)
        {
            if (entry.Level > level) break;
            var result = TryLearnCore(catalog, moves, entry.Move, metadataCreation: true);
            moves = result.Outcome == MoveLearningOutcome.NeedsReplacement
                ? moves.RemoveAt(0).Add(NewSlot(catalog.Moves[entry.Move])) : result.Moves;
        }
        return moves;
    }

    /// <summary>Source candidates at exactly a gained level; repeated entries remain in source order.</summary>
    public static ImmutableArray<MoveId> AtLevel(PokemonSpeciesDefinition species, int level)
    {
        ArgumentNullException.ThrowIfNull(species);
        if (level is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(level));
        if (species.LevelUpMoves.IsDefault) throw new ArgumentException("Missing learnset.");
        return species.LevelUpMoves.Where(x => x.Level == level).Select(x => x.Move).ToImmutableArray();
    }

    /// <summary>Actual level-up does not silently FIFO-replace: a full set returns NeedsReplacement unchanged.</summary>
    public static MoveLearningResult TryLearn(PokemonCatalog catalog, ImmutableArray<PokemonMoveSlot> moves, MoveId move) =>
        TryLearnCore(catalog, moves, move, metadataCreation: false);

    private static MoveLearningResult TryLearnCore(PokemonCatalog catalog, ImmutableArray<PokemonMoveSlot> moves, MoveId move, bool metadataCreation)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ValidateSlots(catalog, moves);
        if (!catalog.Moves.TryGetValue(move, out var definition)) throw new NotSupportedException("Move is outside the imported catalog.");
        if (moves.Any(x => x.Move == move)) return new(MoveLearningOutcome.AlreadyKnown, moves);
        // Return an explicit decision even with a spare slot: the player can decline and keep
        // playing. Never automatically add a move whose remaining PP could prevent Struggle.
        if ((!metadataCreation && !IsLearningSupported(catalog, move)) || moves.Length == 4)
            return new(MoveLearningOutcome.NeedsReplacement, moves);
        return new(MoveLearningOutcome.Added, moves.Add(NewSlot(definition)));
    }

    /// <summary>Explicit chosen replacement; new move has source PP and zero PP Ups.</summary>
    public static ImmutableArray<PokemonMoveSlot> Replace(PokemonCatalog catalog, ImmutableArray<PokemonMoveSlot> moves, int slot, MoveId move)
    {
        ValidateSlots(catalog, moves);
        if (slot < 0 || slot >= moves.Length) throw new ArgumentOutOfRangeException(nameof(slot));
        if (!catalog.Moves.TryGetValue(move, out var definition)) throw new NotSupportedException("Move is outside the imported catalog.");
        if (moves.Any(x => x.Move == move)) throw new ArgumentException("The Pokemon already knows this move.");
        if (!IsLearningSupported(catalog, move)) throw new NotSupportedException("pokemon_learning_effect_unsupported");
        if (IsHiddenMachineMove(moves[slot].Move)) throw new NotSupportedException("Hidden Machine moves cannot be forgotten through ordinary learning.");
        return moves.SetItem(slot, NewSlot(definition));
    }
    private static PokemonMoveSlot NewSlot(PokemonMoveDefinition move) => new(move.Id, move.Pp, move.Pp);
    private static void ValidateSlots(PokemonCatalog catalog, ImmutableArray<PokemonMoveSlot> moves)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (moves.IsDefault || moves.Length > 4) throw new ArgumentException("Use a compact set of zero through four moves.");
        var seen = new HashSet<MoveId>();
        foreach (var slot in moves)
        {
            if (slot is null || !seen.Add(slot.Move) || !catalog.Moves.TryGetValue(slot.Move, out var definition) ||
                slot.PpUps != 0 || slot.MaximumPp != definition.Pp || slot.CurrentPp < 0 || slot.CurrentPp > slot.MaximumPp)
                throw new ArgumentException("Invalid move slots; PP Ups are not supported by this creation/learning profile.");
        }
    }
}
