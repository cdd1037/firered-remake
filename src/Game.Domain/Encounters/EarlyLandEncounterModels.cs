using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Roaming;

namespace Game.Domain.Encounters;

/// <summary>Immutable map encounter definition. Domain validates gameplay values.</summary>
public sealed record EarlyLandEncounterTable(MapId Map, int RawEncounterRate, ImmutableArray<EarlyLandEncounterSlot> Slots);
public sealed record EarlyLandEncounterSlot(int Index, SpeciesId Species, byte MinimumLevel, byte MaximumLevel, int Weight);
public sealed record EarlyLandEncounterCandidate(MapId Map, int Slot, SpeciesId Species, byte Level);
public sealed record PendingEarlyLandEncounter(EarlyLandEncounterCandidate Candidate, byte MetatileBehavior);

/// <summary>Field-effect input. Unbound checks accept only Stench/Illuminate/Cleanse Tag
/// and NONE; catalog-bound checks also authenticate and normalize full lead identifiers.</summary>
public sealed record EarlyLandEncounterProfile(
    string RulesVersion = EarlyLandEncounterRules.FiveIslandRulesVersion,
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
}

/// <summary>Resolve field scripts, trainer sight and warps before emitting a completed field check.</summary>
public sealed record EarlyLandEncounterCheck(MapId Map, EncounterCheckTrigger Trigger,
    byte MetatileBehavior, EncounterTileType EncounterType);

/// <summary>Current walking process state, including pending candidate and source cooldown cache.</summary>
public sealed record EarlyLandEncounterState(
    EncounterRateRngState RateRng,
    ushort PreviousMetatileBehavior = 0,
    ushort EncounterRateBuff = 0,
    byte StepsSinceLastEncounter = 0,
    PendingEarlyLandEncounter? Pending = null,
    string RulesVersion = EarlyLandEncounterRules.FiveIslandRulesVersion)
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
