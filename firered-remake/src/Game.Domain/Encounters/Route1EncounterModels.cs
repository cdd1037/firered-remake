using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

public enum EncounterMovementMode { Walking, Running, Bicycle, Surfing, Forced }
public enum EncounterTileType { None = 0, Land = 1, Water = 2 }
public enum EncounterCheckTrigger { Idle, WalkingStepCompleted, TurnInPlaceCompleted, Blocked, PreemptedByScript }
public enum EncounterCheckOutcome { NoCheck, NoEncounterTerrain, CooldownSuppressed, Disabled, TerrainChangeSuppressed, RateMiss, CandidateReady, RepelSuppressed, RoamerCandidateReady }

/// <summary>Explicit supported context; every modifier outside this bounded profile is rejected.</summary>
public sealed record Route1EncounterProfile(
    string RulesVersion = Route1EncounterRules.RulesVersion,
    EncounterMovementMode Movement = EncounterMovementMode.Walking,
    ushort RepelSteps = 0,
    string LeadAbility = "ABILITY_NONE",
    string LeadHeldItem = "ITEM_NONE",
    bool WhiteFluteActive = false,
    bool BlackFluteActive = false,
    bool RoamerActive = false,
    bool QuestLogPlayback = false,
    bool WildEncountersDisabled = false)
{
    public static Route1EncounterProfile OrdinaryWalking { get; } = new();
}

/// <summary>The field caller must resolve scripts/warps/signs first; this is not a key-press event.</summary>
public sealed record Route1EncounterCheck(MapId Map, EncounterCheckTrigger Trigger,
    byte MetatileBehavior, EncounterTileType EncounterType);

public readonly record struct EncounterRateRngState(uint Seed, ulong Cursor = 0,
    string AlgorithmId = EncounterRateRng.AlgorithmId);

/// <summary>One ordered trace spans both independent source RNG streams.</summary>
public sealed record EncounterRandomTrace(string Stream, RandomTrace Draw);
public sealed record Route1EncounterSlot(int Index, SpeciesId Species, byte MinimumLevel, byte MaximumLevel, int Weight);
public sealed record Route1EncounterCandidate(MapId Map, int Slot, SpeciesId Species, byte Level);
public sealed record PendingRoute1Encounter(Route1EncounterCandidate Candidate, byte MetatileBehavior);

/// <summary>Serializable encounter process state. The session must also retain its separate global RngState.</summary>
public sealed record Route1EncounterState(
    EncounterRateRngState RateRng,
    ushort PreviousMetatileBehavior = 0,
    ushort EncounterRateBuff = 0,
    byte StepsSinceLastEncounter = 0,
    PendingRoute1Encounter? Pending = null,
    string RulesVersion = Route1EncounterRules.RulesVersion);

public sealed record Route1EncounterResult(Route1EncounterState State, RngState GlobalRng,
    EncounterCheckOutcome Outcome, Route1EncounterCandidate? Candidate, int? RateThreshold,
    ImmutableArray<EncounterRandomTrace> RandomTrace);

public sealed record Route1EncounterInitialization(Route1EncounterState State, RngState GlobalRng,
    ImmutableArray<EncounterRandomTrace> RandomTrace);
