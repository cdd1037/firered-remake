using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Rules;

namespace Game.Domain.Battle;

public enum BattleMode { Singles, Doubles }
public enum BattleSide { Player, Opponent }
public enum BattlePhase { AwaitCommands, Ended }
public enum BattleOutcome { Ongoing, PlayerWon, OpponentWon, Draw }
public enum MoveEffect { OrdinaryDamage, Unsupported }
public sealed record MoveDefinition(MoveId Id, ElementType Type, int Power, int Accuracy, int MaxPp,
    int Priority = 0, MoveEffect Effect = MoveEffect.OrdinaryDamage);
public interface IBattleContent { MoveDefinition? FindMove(MoveId id); }
public sealed record MoveSlot(MoveId MoveId, int Pp, int MaxPp);
public sealed record CombatantSnapshot(
    CombatantId Id, SpeciesId SpeciesId, BattleSide Side, int Level, BattleStats Stats,
    ElementType Type1, ElementType? Type2, int Hp, ImmutableArray<MoveSlot> Moves,
    TrainerId? OriginalTrainerId = null);
public sealed record PendingMove(CommandId CommandId, CombatantId ActorId, int MoveSlot, CombatantId TargetId, MoveId MoveId);
public sealed record UseMove(CommandId CommandId, long ExpectedRevision, CombatantId ActorId, int MoveSlot, CombatantId TargetId);

// Arrays permit future multi-active/team profiles. This profile validates exactly one active per side.
public sealed record BattleSnapshot(
    BattleId BattleId, string RulesVersion, string ContentVersion, BattleMode Mode,
    long Revision, int Turn, long EventSequence, BattlePhase Phase, BattleOutcome Outcome,
    ImmutableArray<CombatantSnapshot> Battlers, ImmutableArray<PendingMove> PendingMoves, RngState Rng,
    UnsupportedBattleMechanics ActiveMechanics = UnsupportedBattleMechanics.None)
{
    public static BattleSnapshot Create(BattleId id, string contentVersion, CombatantSnapshot player,
        CombatantSnapshot opponent, uint seed) =>
        new(id, RuleVersions.SingleBattleV0, contentVersion, BattleMode.Singles, 0, 1, 0,
            BattlePhase.AwaitCommands, BattleOutcome.Ongoing, [player, opponent], [], new(seed));
}
public sealed record BattleTransition(TransitionStatus Status, BattleSnapshot State,
    ImmutableArray<BattleEvent> Events, ImmutableArray<RandomTrace> RandomTrace, Diagnostic? Diagnostic);
public sealed record EventMetadata(BattleId BattleId, long Sequence, CommandId CausedByCommandId);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "eventType")]
[JsonDerivedType(typeof(MoveChosenEvent), "moveChosen")]
[JsonDerivedType(typeof(TurnOrderEvent), "turnOrder")]
[JsonDerivedType(typeof(MoveUsedEvent), "moveUsed")]
[JsonDerivedType(typeof(MoveMissedEvent), "moveMissed")]
[JsonDerivedType(typeof(HpChangedEvent), "hpChanged")]
[JsonDerivedType(typeof(FaintedEvent), "fainted")]
[JsonDerivedType(typeof(BattleEndedEvent), "battleEnded")]
[JsonDerivedType(typeof(TurnEndedEvent), "turnEnded")]
public abstract record BattleEvent(EventMetadata Metadata);
public sealed record MoveChosenEvent(EventMetadata Metadata, CombatantId ActorId, MoveId MoveId) : BattleEvent(Metadata);
public sealed record TurnOrderEvent(EventMetadata Metadata, ImmutableArray<CombatantId> Actors) : BattleEvent(Metadata);
public sealed record MoveUsedEvent(EventMetadata Metadata, CombatantId ActorId, CombatantId TargetId, MoveId MoveId,
    int PpBefore, int PpAfter) : BattleEvent(Metadata);
public sealed record MoveMissedEvent(EventMetadata Metadata, CombatantId ActorId, CombatantId TargetId, MoveId MoveId) : BattleEvent(Metadata);
public sealed record HpChangedEvent(EventMetadata Metadata, CombatantId ActorId, CombatantId TargetId, MoveId MoveId,
    int Before, int After, DamageResult Calculation) : BattleEvent(Metadata);
public sealed record FaintedEvent(EventMetadata Metadata, CombatantId ActorId) : BattleEvent(Metadata);
public sealed record BattleEndedEvent(EventMetadata Metadata, BattleOutcome Outcome) : BattleEvent(Metadata);
public sealed record TurnEndedEvent(EventMetadata Metadata, int CompletedTurn) : BattleEvent(Metadata);
