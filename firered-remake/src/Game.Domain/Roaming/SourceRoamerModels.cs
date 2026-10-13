using System.Collections.Immutable;
using Game.Domain.Randomness;

namespace Game.Domain.Roaming;

/// <summary>Source map group/number, not region-map-section IDs.</summary>
public readonly record struct RoamerMap(int Group, int Number);

/// <summary>The source saved individual. OT, EVs, moves and PP are recreated per appearance.</summary>
public sealed record RoamerIndividual(string Species, int Level, uint Personality, uint PackedIvs,
    int CurrentHp, byte Status, bool Active, byte Cool = 0, byte Beauty = 0, byte Cute = 0,
    byte Smart = 0, byte Tough = 0);

/// <summary>Explicitly persisted by the new deterministic campaign profile; source kept these in RAM.
/// History records the PLAYER's maps, newest first. It never records the beast's route history.</summary>
public sealed record RoamerRuntime(RoamerMap Location, ImmutableArray<RoamerMap> PlayerHistory)
{
    public static RoamerRuntime Empty { get; } = new(default, [default, default, default]);
}

/// <summary>Null Individual means never initialized; inactive preserves a defeated/caught identity.</summary>
public sealed record RoamerState(RoamerIndividual? Individual, RoamerRuntime Runtime)
{
    public static RoamerState Empty { get; } = new(null, RoamerRuntime.Empty);
}

public enum RoamerTransition { Connection, Warp }
public sealed record RoamerResult(RoamerState State, RngState Rng, ImmutableArray<RandomTrace> Trace);

public enum RoamerEncounterMethod { Land, Surfing, SweetScentLand, SweetScentWater, Fishing, RockSmash }
public sealed record RoamerPartyMember(int Level, int CurrentHp, bool IsEgg = false);

/// <summary>For ordinary land/surf encounters, the caller runs normal encounter eligibility/rate gates first.
/// Sweet Scent ignores OrdinaryGatesPassed and tries the roamer before HasEncounterTable.</summary>
public sealed record RoamerEncounterContext(RoamerMap PlayerMap, RoamerEncounterMethod Method,
    bool OrdinaryGatesPassed, bool HasEncounterTable, bool RepelActive,
    ImmutableArray<RoamerPartyMember> Party);

public enum RoamerEncounterDecision
{
    NotApplicable, NoEncounter, UseOrdinaryEncounter, RoamerEncounter
}

/// <summary>Battle construction must use this fixed PID/full IV identity without draws, fresh source
/// moves/PP and zero EVs, then restore HP/status/contest fields. This is not battle admission.</summary>
public sealed record RoamerEncounterCandidate(RoamerIndividual Individual, ImmutableArray<int> Ivs,
    int MaximumHp);

public sealed record RoamerEncounterResult(RoamerEncounterDecision Decision,
    RoamerEncounterCandidate? Candidate, RngState Rng, ImmutableArray<RandomTrace> Trace);

// These numeric values are the source outcome constants, not the campaign's result enum.
public enum RoamerBattleOutcome
{
    Won = 1, Lost = 2, Drew = 3, Ran = 4, PlayerTeleported = 5,
    MonFled = 6, Caught = 7, NoSafariBalls = 8, Forfeited = 9, MonTeleported = 10
}

/// <summary>EncounteredIndividual binds settlement to the exact original saved individual.
/// Status is the source 32-bit battle status; only the low byte persists in roamer state.</summary>
public sealed record RoamerBattleResult(RoamerIndividual EncounteredIndividual, int CurrentHp,
    uint Status, RoamerBattleOutcome Outcome);
