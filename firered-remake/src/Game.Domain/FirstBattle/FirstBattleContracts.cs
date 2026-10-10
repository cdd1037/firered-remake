using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.FirstBattle;

public enum FirstBattleSide { Player, Opponent }
public enum FirstBattleOutcome { Ongoing = 0, Won = 1, Lost = 2, Draw = 3 }
[Flags]
public enum FirstBattleTutorialFlags { None = 0, Damage = 1, StatChange = 2, HpRestore = 4, PartyMenu = 8 }
public enum FirstBattleOrderPhase { Entry, Actions, EndTurn }
public enum FirstBattleStat { Attack, Defense }

/// <summary>Only represented first-lab-battle conditions are accepted; this is not a general battle mon.</summary>
public sealed record FirstBattleCombatant(PokemonInstance Pokemon, int AttackStage = 6, int DefenseStage = 6,
    string HeldItem = "ITEM_NONE", uint Status = 0);

/// <summary>One semantic RNG stream, including AI draws supplied before resolving a turn. Source stage 6 is neutral.</summary>
public sealed record FirstBattleState(FirstBattleCombatant Player, FirstBattleCombatant Opponent,
    FirstBattleTutorialFlags TutorialFlags, int Turn, long Revision, long EventSequence, RngState Rng,
    ushort RandomTurnNumber, FirstBattleOutcome Outcome, string RulesVersion = FirstBattleRules.RulesVersion);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(FirstBattleStartedEvent), "started")]
[JsonDerivedType(typeof(FirstBattleOrderEvent), "order")]
[JsonDerivedType(typeof(FirstBattleMoveUsedEvent), "move-used")]
[JsonDerivedType(typeof(FirstBattleMoveMissedEvent), "move-missed")]
[JsonDerivedType(typeof(FirstBattleHpChangedEvent), "hp-changed")]
[JsonDerivedType(typeof(FirstBattlePotionRecoveredEvent), "potion-recovered")]
[JsonDerivedType(typeof(FirstBattleRecoilEvent), "recoil")]
[JsonDerivedType(typeof(FirstBattleCriticalHitEvent), "critical-hit")]
[JsonDerivedType(typeof(FirstBattleStatChangedEvent), "stat-changed")]
[JsonDerivedType(typeof(FirstBattleStatAtMinimumEvent), "stat-at-minimum")]
[JsonDerivedType(typeof(FirstBattleTutorialEvent), "tutorial")]
[JsonDerivedType(typeof(FirstBattleFaintedEvent), "fainted")]
[JsonDerivedType(typeof(FirstBattleEndedEvent), "ended")]
[JsonDerivedType(typeof(FirstBattleTurnReadyEvent), "turn-ready")]
public abstract record FirstBattleEvent(long Sequence, int Turn);
public sealed record FirstBattleStartedEvent(long Sequence, int Turn) : FirstBattleEvent(Sequence, Turn);
public sealed record FirstBattleOrderEvent(long Sequence, int Turn, FirstBattleOrderPhase Phase,
    FirstBattleSide First, FirstBattleSide Second) : FirstBattleEvent(Sequence, Turn);
public sealed record FirstBattleMoveUsedEvent(long Sequence, int Turn, FirstBattleSide Actor,
    FirstBattleSide Target, int Slot, MoveId Move, int PpBefore, int PpAfter) : FirstBattleEvent(Sequence, Turn);
public sealed record FirstBattleMoveMissedEvent(long Sequence, int Turn, FirstBattleSide Actor,
    FirstBattleSide Target, MoveId Move) : FirstBattleEvent(Sequence, Turn);
public sealed record FirstBattleHpChangedEvent(long Sequence, int Turn, FirstBattleSide Actor,
    FirstBattleSide Target, MoveId Move, int HpBefore, int HpAfter, int Damage, bool Critical,
    int RandomPercent) : FirstBattleEvent(Sequence, Turn);
/// <summary>Recovery already applied by the Party-menu item transaction, before the HP tutorial and action order.</summary>
public sealed record FirstBattlePotionRecoveredEvent(long Sequence, int Turn, FirstBattleSide Side,
    int HpBefore, int HpAfter, int HpRestored) : FirstBattleEvent(Sequence, Turn);
/// <summary>Source Struggle recoil: max(1, actual target HP removed / 4), before attacker then target faint checks.</summary>
public sealed record FirstBattleRecoilEvent(long Sequence, int Turn, FirstBattleSide Actor,
    MoveId Move, int HpBefore, int HpAfter, int Damage) : FirstBattleEvent(Sequence, Turn);
public sealed record FirstBattleCriticalHitEvent(long Sequence, int Turn, FirstBattleSide Actor,
    FirstBattleSide Target) : FirstBattleEvent(Sequence, Turn);
public sealed record FirstBattleStatChangedEvent(long Sequence, int Turn, FirstBattleSide Actor,
    FirstBattleSide Target, FirstBattleStat Stat, int Before, int After) : FirstBattleEvent(Sequence, Turn);
public sealed record FirstBattleStatAtMinimumEvent(long Sequence, int Turn, FirstBattleSide Actor,
    FirstBattleSide Target, FirstBattleStat Stat) : FirstBattleEvent(Sequence, Turn);
public sealed record FirstBattleTutorialEvent(long Sequence, int Turn, FirstBattleTutorialFlags Flag,
    FirstBattleTutorialFlags FlagsAfter) : FirstBattleEvent(Sequence, Turn);
public sealed record FirstBattleFaintedEvent(long Sequence, int Turn, FirstBattleSide Side) : FirstBattleEvent(Sequence, Turn);
public sealed record FirstBattleEndedEvent(long Sequence, int Turn, FirstBattleOutcome Outcome) : FirstBattleEvent(Sequence, Turn);
public sealed record FirstBattleTurnReadyEvent(long Sequence, int Turn, ushort RandomTurnNumber) : FirstBattleEvent(Sequence, Turn);

/// <summary>On fault no draws/events commit; turn resolvers return their original state, Start returns null.</summary>
public sealed record FirstBattleResult(TransitionStatus Status, FirstBattleState? State,
    ImmutableArray<FirstBattleEvent> Events, ImmutableArray<RandomTrace> Trace, Diagnostic? Diagnostic)
{
    public bool Succeeded => Status == TransitionStatus.Accepted;
    public string? Fault => Diagnostic?.Code;
}
