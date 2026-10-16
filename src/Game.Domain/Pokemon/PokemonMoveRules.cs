using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

public static class PokemonMoveRules
{
    /// <summary>Ordinary learning cannot forget HM moves (IsHMMove2 in the original learn-move menu).</summary>
    public static bool IsHiddenMachineMove(MoveId move) => move.Value is "pret:MOVE_CUT" or "pret:MOVE_FLY" or "pret:MOVE_SURF" or "pret:MOVE_STRENGTH" or "pret:MOVE_FLASH" or "pret:MOVE_ROCK_SMASH" or "pret:MOVE_WATERFALL" or "pret:MOVE_DIVE";

    /// <summary>The gift profile keeps every source move as metadata while deferring learning of unimplemented battle effects.</summary>
    public static bool IsLearningSupported(PokemonCatalog catalog, MoveId move) =>
        catalog.Moves.TryGetValue(move, out var sourceMove) &&
        Game.Domain.WildBattle.WildBattleRules.IsCalledMoveEffectSupported(sourceMove);

    /// <summary>CreateBoxMon learns in source order and automatically forgets the oldest move.
    /// Day Care later uses the same source automatic-learning primitive.</summary>
    public static ImmutableArray<PokemonMoveSlot> InitialMoves(PokemonCatalog catalog, PokemonSpeciesDefinition species, int level)
    {
        PokemonFactory.ValidateCatalog(catalog);
        return InitialMovesCore(catalog, species, level);
    }

    internal static ImmutableArray<PokemonMoveSlot> InitialMovesCore(PokemonCatalog catalog, PokemonSpeciesDefinition species, int level)
    {
        if (level is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(level));
        ArgumentNullException.ThrowIfNull(species);
        if (!catalog.Species.TryGetValue(species.Id, out var registered) || registered != species)
            throw new ArgumentException("Species must be the catalog definition.");
        ImmutableArray<PokemonMoveSlot> moves = [];
        foreach (var entry in species.LevelUpMoves)
        {
            if (entry.Level > level) break;
            moves = LearnAutomatically(catalog, moves, entry.Move);
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

    /// <summary>Source Day Care uses GiveMoveToMon then DeleteFirstMoveAndGiveMoveToMon.
    /// Unlike the player's learning menu this can forget an HM; retained PP bonuses move with
    /// their slots, and a newly learned move has full base PP and no bonuses.</summary>
    internal static ImmutableArray<PokemonMoveSlot> LearnAutomatically(PokemonCatalog catalog,
        ImmutableArray<PokemonMoveSlot> moves, MoveId move)
    {
        var learned = TryLearnCore(catalog, moves, move, metadataCreation: true);
        return learned.Outcome == MoveLearningOutcome.NeedsReplacement
            ? moves.RemoveAt(0).Add(NewSlot(catalog.Moves[move])) : learned.Moves;
    }

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
    /// <summary>CalculatePPWithBonus from the fixed source; caller validates the source definition and 0–3 bonus.</summary>
    internal static int MaximumPp(PokemonMoveDefinition move, int ppUps) => move.Pp + move.Pp * 20 * ppUps / 100;

    private static PokemonMoveSlot NewSlot(PokemonMoveDefinition move) => new(move.Id, move.Pp, move.Pp);
    private static void ValidateSlots(PokemonCatalog catalog, ImmutableArray<PokemonMoveSlot> moves)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (moves.IsDefault || moves.Length > 4) throw new ArgumentException("Use a compact set of zero through four moves.");
        // Preserve the historical zero-bonus guard. Only authenticated source learning admits
        // existing PP bonuses; NewSlot still clears a newly learned/replaced move to zero.
        bool canonical = catalog.MechanicsData is not null;
        var seen = new HashSet<MoveId>();
        foreach (var slot in moves)
        {
            if (slot is null || !seen.Add(slot.Move) || !catalog.Moves.TryGetValue(slot.Move, out var definition) ||
                slot.PpUps is < 0 or > 3 || (!canonical && slot.PpUps != 0) ||
                slot.MaximumPp != MaximumPp(definition, slot.PpUps) ||
                slot.CurrentPp < 0 || slot.CurrentPp > slot.MaximumPp)
                throw new ArgumentException(canonical ? "Invalid canonical move slots or PP bonuses." :
                    "Invalid move slots; PP Ups are not supported by this creation/learning profile.");
        }
    }
}
