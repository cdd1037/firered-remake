using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Original rod IDs; admission is authenticated independently from the shared task.</summary>
public enum FishingRod { Old = 0, Good = 1, Super = 2 }

public enum OldRodFishingStatus { Running, NotEvenANibble, EncounterReady, Cancelled, Rejected }

/// <summary>Source-resolved collision is GetCollisionAtCoords, not the raw map collision bits.</summary>
public sealed record OldRodCastContext(MapId Map, bool OwnsOldRod, int PlayerElevation,
    int FrontTerrain, int FrontMetatileBehavior, int FrontCollision, bool IsSurfing = false,
    bool IsUnderwater = false)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public FishingRod Rod { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool OwnsGoodRod { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool OwnsSuperRod { get; init; }
}

/// <summary>One Task_Fishing invocation; completion inputs acknowledge source presentation boundaries.</summary>
public sealed record OldRodFrameInput(bool APressed = false, bool BPressed = false,
    bool MessageFinished = false, bool NoCatchAnimationFinished = false);

public sealed record OldRodFishingState(MapId Map, bool HasFishingMons, int SourceStep = 1,
    int FrameCounter = 0, int NumDots = 0, int DotsRequired = 0, int RoundsPlayed = 0,
    int MinimumRoundsRequired = 0, OldRodFishingStatus Status = OldRodFishingStatus.Running,
    string RulesVersion = OldRodFishingRules.RulesVersion)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public FishingRod Rod { get; init; }
}

public sealed record OldRodFishingResult(OldRodFishingState State, RngState GlobalRng,
    ImmutableArray<RandomTrace> RandomTrace);

public sealed record OldRodEncounterSlot(int Index, SpeciesId Species, byte MinimumLevel,
    byte MaximumLevel, int Weight);
public sealed record OldRodEncounterTable(MapId Map, string SourceCommit, string SourcePath,
    string SourceSymbol, int RawEncounterRate, ImmutableArray<OldRodEncounterSlot> Slots);
public sealed record OldRodEncounterCandidate(MapId Map, int SlotIndex, SpeciesId Species, byte Level);
public sealed record OldRodEncounterResult(OldRodEncounterCandidate Candidate, RngState GlobalRng,
    ImmutableArray<RandomTrace> RandomTrace);
public sealed record OldRodPokemonResult(PokemonInstance Pokemon, RngState GlobalRng,
    ImmutableArray<RandomTrace> RandomTrace);
public sealed record OldRodHeldItemResult(string HeldItem, RngState GlobalRng,
    ImmutableArray<RandomTrace> RandomTrace);
