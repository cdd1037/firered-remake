using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Roaming;

namespace Game.Domain.Encounters;

public sealed record WaterEncounterSlot(int Index, SpeciesId Species, byte MinimumLevel, byte MaximumLevel, int Weight);
public sealed record WaterEncounterTable(MapId Map, string SourceMapSymbol, string SourceCommit, string SourcePath,
    string SourceSymbol, int RawEncounterRate, ImmutableArray<WaterEncounterSlot> Slots);
public sealed record WaterEncounterCandidate(MapId Map, int Slot, SpeciesId Species, byte Level);
public sealed record PendingWaterEncounter(WaterEncounterCandidate Candidate, byte MetatileBehavior);
public sealed record WaterEncounterState(EncounterRateRngState RateRng, ushort PreviousMetatileBehavior = 0,
    ushort EncounterRateBuff = 0, byte StepsSinceLastEncounter = 0, PendingWaterEncounter? Pending = null,
    string RulesVersion = WaterEncounterRules.RulesVersion)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool CachedLeadHasCleanseTag { get; init; }
}
public sealed record WaterEncounterResult(WaterEncounterState State, RngState GlobalRng, EncounterCheckOutcome Outcome,
    WaterEncounterCandidate? Candidate, int? RateThreshold, ImmutableArray<EncounterRandomTrace> RandomTrace)
{
    /// <summary>Transient replacement candidate; commit its battle start atomically with this result.
    /// It is never represented as an ordinary table slot or persisted as an ordinary pending candidate.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RoamerEncounterCandidate? RoamerCandidate { get; init; }
}
