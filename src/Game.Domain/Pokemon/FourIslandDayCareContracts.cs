using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.Pokemon;

/// <summary>The independent Four Island DayCare. Original offspringPersonality is only a ushort:
/// zero means no egg; the high PID half is drawn later, when receiving the egg.</summary>
public sealed record FourIslandDayCareState(SourceDayCareResident? First, SourceDayCareResident? Second,
    ushort OffspringPersonality, byte HatchStepCounter)
{
    public static FourIslandDayCareState Empty { get; } = new(null, null, 0, 0);
    [JsonIgnore] public bool HasPendingEgg => OffspringPersonality != 0;
    [JsonIgnore] public int Count => (First is null ? 0 : 1) + (Second is null ? 0 : 1);
}

/// <summary>Original species_info fields. The authenticated Content owner must provide the
/// current owned-species table; this is not a second ownership catalog or a new Pokemon model.</summary>
public sealed record SourceBreedingSpecies(SpeciesId Species, int EggGroup1, int EggGroup2, byte EggCycles);

public sealed record FourIslandDayCareResult(FourIslandDayCareState State, ImmutableArray<PartyPokemon> Party,
    SourceMailState Mail, bool Succeeded, string? Error, SourceDayCareQuote? Quote = null,
    int MoneyPaid = 0, ImmutableArray<SourceDayCareLearnedMove> LearnedMoves = default);

/// <summary>One original ShouldEggHatch admission. HatchCycleDue means scan party order now:
/// decrement a positive cycle, or hatch the first already-zero egg and stop scanning.
/// This candidate does not create, admit or mutate party eggs.</summary>
public sealed record FourIslandDayCareStepResult(FourIslandDayCareState State, RngState Rng,
    ImmutableArray<RandomTrace> Trace, bool EggProduced, bool HatchCycleDue,
    int? Compatibility = null, int? ChanceRoll = null);
