using System.Collections.Immutable;
using Game.Domain.Randomness;

namespace Game.Domain.Capture;

/// <summary>Values match source item IDs, not ball animation IDs.</summary>
public enum CaptureBall
{
    Master = 1, Ultra = 2, Great = 3, Poke = 4,
    Safari = 5, Net = 6, Dive = 7, Nest = 8, Repeat = 9, Timer = 10, Luxury = 11, Premier = 12
}

/// <summary>Exactly one legal major status. Sleep duration and toxic counter do not alter this calculation.</summary>
public enum CaptureStatus { None, Sleep, Freeze, Poison, Burn, Paralysis, BadPoison }
public enum CaptureEncounterKind { OrdinaryWild, Trainer, Ghost, Tutorial, Safari, Link, Double, Roamer, Story }
public enum CaptureOutcome { Caught, Escaped, InvalidInput, Unsupported }
public enum CaptureDecisionBranch { GuaranteedCatchValue, ShakeChecks, MasterBallOverride }

public readonly record struct CaptureInput(
    int MaximumHp,
    int CurrentHp,
    int CatchRate,
    CaptureBall Ball,
    CaptureStatus Status = CaptureStatus.None,
    CaptureEncounterKind Encounter = CaptureEncounterKind.OrdinaryWild);

/// <summary>
/// Live battle inputs for conditional balls. TargetIsWaterOrBug reads the effective battle types;
/// TargetSpeciesCaught refers to the effective battle species, including Transform. IsUnderwater
/// means MAP_TYPE_UNDERWATER, never a cave, fishing encounter, Water terrain or the move Dive.
/// BattleTurnCounter is the source's saturating byte counter of completed turns (first turn: 0).
/// </summary>
public sealed record CaptureBallContext(int TargetLevel, bool TargetIsWaterOrBug,
    bool IsUnderwater, bool TargetSpeciesCaught, int BattleTurnCounter);

/// <summary>Intermediate integer values, so rounding and the guaranteed-capture branch are inspectable.</summary>
public sealed record CaptureCalculation(
    int BallMultiplierTenths,
    uint BallAdjustedCatchRate,
    uint HpAdjustedCatchValue,
    uint CatchValue,
    uint? ShakeThreshold);

/// <summary>
/// No bag, battle, party, storage, Pokédex or Pokémon mutation is performed.
/// AnimationCode 0..3 is failure after that many passed checks; 4 means three-shake success.
/// ChecksPassed counts actual successful comparisons, not animation shakes or Master Ball overrides.
/// Invalid/unsupported results retain the exact input RNG and contain no draws or calculation.
/// </summary>
public sealed record CaptureResult(
    CaptureOutcome Outcome,
    string? Diagnostic,
    CaptureDecisionBranch? Branch,
    CaptureCalculation? Calculation,
    int ChecksPassed,
    int? AnimationCode,
    RngState Rng,
    ImmutableArray<RandomTrace> RandomTrace);
