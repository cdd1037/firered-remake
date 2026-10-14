using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Roaming;

namespace Game.Domain.Encounters;

/// <summary>Typed table supplied by authenticated Content. Domain checks structure, not file authenticity.</summary>
public sealed record EarlyLandEncounterTable(MapId Map, string SourceCommit, string SourcePath,
    string SourceSymbol, string SourceSha256, int RawEncounterRate, ImmutableArray<EarlyLandEncounterSlot> Slots);
public sealed record EarlyLandEncounterSlot(int Index, SpeciesId Species, byte MinimumLevel, byte MaximumLevel, int Weight);
public sealed record EarlyLandEncounterCandidate(MapId Map, int Slot, SpeciesId Species, byte Level);
public sealed record PendingEarlyLandEncounter(EarlyLandEncounterCandidate Candidate, byte MetatileBehavior);

public sealed record EarlyLandEncounterProfile(
    string RulesVersion = EarlyLandEncounterRules.RulesVersion,
    EncounterMovementMode Movement = EncounterMovementMode.Walking,
    ushort RepelSteps = 0,
    string LeadAbility = "ABILITY_NONE",
    string LeadHeldItem = "ITEM_NONE",
    bool WhiteFluteActive = false,
    bool BlackFluteActive = false,
    bool RoamerActive = false,
    bool QuestLogPlayback = false,
    bool WildEncountersDisabled = false,
    string Weather = "WEATHER_NONE")
{
    public static EarlyLandEncounterProfile OrdinaryWalking { get; } = new();
    public static EarlyLandEncounterProfile MtMoonWalking { get; } = new(RulesVersion: EarlyLandEncounterRules.MtMoonRulesVersion);
    public static EarlyLandEncounterProfile Route22Walking { get; } = new(RulesVersion: EarlyLandEncounterRules.Route22RulesVersion);
}

/// <summary>Resolve field scripts, trainer sight and warps before emitting a completed field check.</summary>
public sealed record EarlyLandEncounterCheck(MapId Map, EncounterCheckTrigger Trigger,
    byte MetatileBehavior, EncounterTileType EncounterType);

/// <summary>A separately versioned process; old Route 1 saves are not reinterpreted as this state.</summary>
public sealed record EarlyLandEncounterState(
    EncounterRateRngState RateRng,
    ushort PreviousMetatileBehavior = 0,
    ushort EncounterRateBuff = 0,
    byte StepsSinceLastEncounter = 0,
    PendingEarlyLandEncounter? Pending = null,
    string RulesVersion = EarlyLandEncounterRules.RulesVersion)
{
    // Source HandleWildEncounterCooldown caches the lead held item before its cooldown roll.
    // RockSmashWildEncounter reads that cache without resampling the party.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool CachedLeadHasCleanseTag { get; init; }
}

public sealed record EarlyLandEncounterResult(EarlyLandEncounterState State, RngState GlobalRng,
    EncounterCheckOutcome Outcome, EarlyLandEncounterCandidate? Candidate, int? RateThreshold,
    ImmutableArray<EncounterRandomTrace> RandomTrace)
{
    /// <summary>Transient replacement candidate; commit its battle start atomically with this result.
    /// It is never represented as an ordinary table slot or persisted as an ordinary pending candidate.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RoamerEncounterCandidate? RoamerCandidate { get; init; }
}
public sealed record EarlyLandEncounterInitialization(EarlyLandEncounterState State, RngState GlobalRng,
    ImmutableArray<EncounterRandomTrace> RandomTrace);
