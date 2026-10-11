using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

public sealed record WaterEncounterSlot(int Index, SpeciesId Species, byte MinimumLevel, byte MaximumLevel, int Weight);
public sealed record WaterEncounterTable(MapId Map, string SourceMapSymbol, string SourceCommit, string SourcePath,
    string SourceSymbol, string SourceSha256, int RawEncounterRate, ImmutableArray<WaterEncounterSlot> Slots);
public sealed record WaterEncounterCandidate(MapId Map, int Slot, SpeciesId Species, byte Level);
public sealed record PendingWaterEncounter(WaterEncounterCandidate Candidate, byte MetatileBehavior);
public sealed record WaterEncounterState(EncounterRateRngState RateRng, ushort PreviousMetatileBehavior = 0,
    ushort EncounterRateBuff = 0, byte StepsSinceLastEncounter = 0, PendingWaterEncounter? Pending = null,
    string RulesVersion = WaterEncounterRules.RulesVersion);
public sealed record WaterEncounterResult(WaterEncounterState State, RngState GlobalRng, EncounterCheckOutcome Outcome,
    WaterEncounterCandidate? Candidate, int? RateThreshold, ImmutableArray<EncounterRandomTrace> RandomTrace);
