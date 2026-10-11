using System.Collections.Immutable;
using Game.Domain.Capture;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Safari;

public enum SafariAction { Ball, Bait, Rock, Run }
public enum SafariBattleOutcome { Active, Caught, Fled, Ran, OutOfBalls }
public enum SafariReaction { Watching, Eating, Angry }

/// <summary>
/// A separate Safari encounter, with no player battler, damage, PP use, EXP or bag actions.
/// Target is the original healthy generated instance. Application owns encounter/visit admission,
/// held-item/met/caught-ball receipts, party/PC delivery and the field's 600-step visit.
/// EatingTurns/AngryTurns are source counters, not promised future action counts.
/// Rng is at the ready-for-selection boundary, after gRandomTurnNumber was drawn.
/// </summary>
public sealed record SafariBattleState(PokemonInstance Target, int BallsRemaining,
    int CatchFactor, int EscapeFactor, int EatingTurns, int AngryTurns,
    int BattleTurnCounter, SafariBattleOutcome Outcome, RngState Rng);

public abstract record SafariBattleEvent;
public sealed record SafariActionEvent(SafariAction Action, int BallsRemaining) : SafariBattleEvent;
public sealed record SafariMoodChangedEvent(SafariAction Action, int DurationAdded,
    int EatingTurns, int AngryTurns, int CatchFactor) : SafariBattleEvent;
public sealed record SafariCaptureEvent(CaptureResult Capture, int BallsRemaining) : SafariBattleEvent;
public sealed record SafariReactionEvent(SafariReaction Reaction, int EatingTurns,
    int AngryTurns, int CatchFactor) : SafariBattleEvent;
public sealed record SafariBattleEndedEvent(SafariBattleOutcome Outcome) : SafariBattleEvent;

/// <summary>AI decision uses counters at the start of this round, before the player's action.</summary>
public sealed record SafariFleeDecision(int ChancePercent, int Roll, bool SelectedFlee);

/// <summary>Rejected transactions preserve the input state and RNG, with no events or draws.</summary>
public sealed record SafariBattleResult(bool Succeeded, string? Fault, SafariBattleState? State,
    ImmutableArray<SafariBattleEvent> Events, ImmutableArray<RandomTrace> RandomTrace,
    SafariFleeDecision? FleeDecision = null);
