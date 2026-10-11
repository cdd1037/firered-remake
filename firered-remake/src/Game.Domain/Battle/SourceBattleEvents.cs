using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Capture;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Typed canonical events. Shared hooks consume positions; singles conversion occurs only at a boundary.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SourceRewardAppliedEvent), "SourceRewardAppliedEvent")]
[JsonDerivedType(typeof(SourceRewardLearningEvent), "SourceRewardLearningEvent")]
[JsonDerivedType(typeof(SourceOpponentRewardCompletedEvent), "SourceOpponentRewardCompletedEvent")]
[JsonDerivedType(typeof(SourcePermanentWeatherChangedEvent), "SourcePermanentWeatherChangedEvent")]
[JsonDerivedType(typeof(SourceAbilityRecoveredEvent), "SourceAbilityRecoveredEvent")]
[JsonDerivedType(typeof(SourceAbilityActivatedEvent), "SourceAbilityActivatedEvent")]
[JsonDerivedType(typeof(SourceAbilityDamageEvent), "SourceAbilityDamageEvent")]
[JsonDerivedType(typeof(SourceAbilityPreventedEvent), "SourceAbilityPreventedEvent")]
[JsonDerivedType(typeof(SourceActionBlockedEvent), "SourceActionBlockedEvent")]
[JsonDerivedType(typeof(SourceAttackStringEvent), "SourceAttackStringEvent")]
[JsonDerivedType(typeof(SourceBallThrownEvent), "SourceBallThrownEvent")]
[JsonDerivedType(typeof(SourceBindingChangedEvent), "SourceBindingChangedEvent")]
[JsonDerivedType(typeof(SourceBindingDamageEvent), "SourceBindingDamageEvent")]
[JsonDerivedType(typeof(SourceCalledBeatUpMemberEvent), "SourceCalledBeatUpMemberEvent")]
[JsonDerivedType(typeof(SourceCalledDelayedEvent), "SourceCalledDelayedEvent")]
[JsonDerivedType(typeof(SourceCalledMoveTransitionEvent), "SourceCalledMoveTransitionEvent")]
[JsonDerivedType(typeof(SourceCalledOverlayChangedEvent), "SourceCalledOverlayChangedEvent")]
[JsonDerivedType(typeof(SourceCalledPartyChangedEvent), "SourceCalledPartyChangedEvent")]
[JsonDerivedType(typeof(SourceCalledPartySwitchRequiredEvent), "SourceCalledPartySwitchRequiredEvent")]
[JsonDerivedType(typeof(SourceCalledPartySwitchedEvent), "SourceCalledPartySwitchedEvent")]
[JsonDerivedType(typeof(SourceCalledSpikesChangedEvent), "SourceCalledSpikesChangedEvent")]
[JsonDerivedType(typeof(SourceCallerSelectionEvent), "SourceCallerSelectionEvent")]
[JsonDerivedType(typeof(SourceCaptureResultEvent), "SourceCaptureResultEvent")]
[JsonDerivedType(typeof(SourceConfusionChangedEvent), "SourceConfusionChangedEvent")]
[JsonDerivedType(typeof(SourceConfusionSelfHitEvent), "SourceConfusionSelfHitEvent")]
[JsonDerivedType(typeof(SourceCriticalHitEvent), "SourceCriticalHitEvent")]
[JsonDerivedType(typeof(SourceDefenseCurledEvent), "SourceDefenseCurledEvent")]
[JsonDerivedType(typeof(SourceDisableChangedEvent), "SourceDisableChangedEvent")]
[JsonDerivedType(typeof(SourceDrainEvent), "SourceDrainEvent")]
[JsonDerivedType(typeof(SourceEffectivenessEvent), "SourceEffectivenessEvent")]
[JsonDerivedType(typeof(SourceEncoreChangedEvent), "SourceEncoreChangedEvent")]
[JsonDerivedType(typeof(SourceEndedEvent), "SourceEndedEvent")]
[JsonDerivedType(typeof(SourceFaintedEvent), "SourceFaintedEvent")]
[JsonDerivedType(typeof(SourceFlinchEvent), "SourceFlinchEvent")]
[JsonDerivedType(typeof(SourceFocusEnergyEvent), "SourceFocusEnergyEvent")]
[JsonDerivedType(typeof(SourceFocusPunchLostEvent), "SourceFocusPunchLostEvent")]
[JsonDerivedType(typeof(SourceFocusPunchPreparedEvent), "SourceFocusPunchPreparedEvent")]
[JsonDerivedType(typeof(SourceForcedOutEvent), "SourceForcedOutEvent")]
[JsonDerivedType(typeof(SourceGrudgePpEvent), "SourceGrudgePpEvent")]
[JsonDerivedType(typeof(SourceHeldItemConsumedEvent), "SourceHeldItemConsumedEvent")]
[JsonDerivedType(typeof(SourceHeldItemKnockedOffEvent), "SourceHeldItemKnockedOffEvent")]
[JsonDerivedType(typeof(SourceHeldItemStolenEvent), "SourceHeldItemStolenEvent")]
[JsonDerivedType(typeof(SourceHpChangedEvent), "SourceHpChangedEvent")]
[JsonDerivedType(typeof(SourceInfatuationBlockedEvent), "SourceInfatuationBlockedEvent")]
[JsonDerivedType(typeof(SourceInfatuationChangedEvent), "SourceInfatuationChangedEvent")]
[JsonDerivedType(typeof(SourceIngrainHealEvent), "SourceIngrainHealEvent")]
[JsonDerivedType(typeof(SourceLeechSeedChangedEvent), "SourceLeechSeedChangedEvent")]
[JsonDerivedType(typeof(SourceLeechSeedDrainEvent), "SourceLeechSeedDrainEvent")]
[JsonDerivedType(typeof(SourceMagicCoatEvent), "SourceMagicCoatEvent")]
[JsonDerivedType(typeof(SourceMagnitudeEvent), "SourceMagnitudeEvent")]
[JsonDerivedType(typeof(SourceMedicineAppliedEvent), "SourceMedicineAppliedEvent")]
[JsonDerivedType(typeof(SourceMoveCopiedEvent), "SourceMoveCopiedEvent")]
[JsonDerivedType(typeof(SourceMoveFailedEvent), "SourceMoveFailedEvent")]
[JsonDerivedType(typeof(SourceMoveMissedEvent), "SourceMoveMissedEvent")]
[JsonDerivedType(typeof(SourceMoveSnatchedEvent), "SourceMoveSnatchedEvent")]
[JsonDerivedType(typeof(SourceMoveUsedEvent), "SourceMoveUsedEvent")]
[JsonDerivedType(typeof(SourceMtMoonVolatileEvent), "SourceMtMoonVolatileEvent")]
[JsonDerivedType(typeof(SourceMultiHitEvent), "SourceMultiHitEvent")]
[JsonDerivedType(typeof(SourceOrderEvent), "SourceOrderEvent")]
[JsonDerivedType(typeof(SourcePayDayEvent), "SourcePayDayEvent")]
[JsonDerivedType(typeof(SourcePlayerReplacedEvent), "SourcePlayerReplacedEvent")]
[JsonDerivedType(typeof(SourcePlayerSwitchedEvent), "SourcePlayerSwitchedEvent")]
[JsonDerivedType(typeof(SourcePokeFluteEvent), "SourcePokeFluteEvent")]
[JsonDerivedType(typeof(SourcePotionRecoveredEvent), "SourcePotionRecoveredEvent")]
[JsonDerivedType(typeof(SourcePpCommandEvent), "SourcePpCommandEvent")]
[JsonDerivedType(typeof(SourceProtectAttemptEvent), "SourceProtectAttemptEvent")]
[JsonDerivedType(typeof(SourcePursuitInterceptedEvent), "SourcePursuitInterceptedEvent")]
[JsonDerivedType(typeof(SourceRecoilEvent), "SourceRecoilEvent")]
[JsonDerivedType(typeof(SourceRecoveredEvent), "SourceRecoveredEvent")]
[JsonDerivedType(typeof(SourceReplacementRequiredEvent), "SourceReplacementRequiredEvent")]
[JsonDerivedType(typeof(SourceRolloutChangedEvent), "SourceRolloutChangedEvent")]
[JsonDerivedType(typeof(SourceRootsChangedEvent), "SourceRootsChangedEvent")]
[JsonDerivedType(typeof(SourceRunAttemptEvent), "SourceRunAttemptEvent")]
[JsonDerivedType(typeof(SourceSelfDestructEvent), "SourceSelfDestructEvent")]
[JsonDerivedType(typeof(SourceSleepTalkEvent), "SourceSleepTalkEvent")]
[JsonDerivedType(typeof(SourceSnatchPressurePpEvent), "SourceSnatchPressurePpEvent")]
[JsonDerivedType(typeof(SourceSnatchReadyEvent), "SourceSnatchReadyEvent")]
[JsonDerivedType(typeof(SourceSpiteEvent), "SourceSpiteEvent")]
[JsonDerivedType(typeof(SourceStartedEvent), "SourceStartedEvent")]
[JsonDerivedType(typeof(SourceStatAtMaximumEvent), "SourceStatAtMaximumEvent")]
[JsonDerivedType(typeof(SourceStatAtMinimumEvent), "SourceStatAtMinimumEvent")]
[JsonDerivedType(typeof(SourceStatChangedEvent), "SourceStatChangedEvent")]
[JsonDerivedType(typeof(SourceStatusChangedEvent), "SourceStatusChangedEvent")]
[JsonDerivedType(typeof(SourceStatusDamageEvent), "SourceStatusDamageEvent")]
[JsonDerivedType(typeof(SourceSubstituteDamagedEvent), "SourceSubstituteDamagedEvent")]
[JsonDerivedType(typeof(SourceTowerBallDodgedEvent), "SourceTowerBallDodgedEvent")]
[JsonDerivedType(typeof(SourceTowerGhostActionEvent), "SourceTowerGhostActionEvent")]
[JsonDerivedType(typeof(SourceTowerHpEvent), "SourceTowerHpEvent")]
[JsonDerivedType(typeof(SourceTowerVolatileEvent), "SourceTowerVolatileEvent")]
[JsonDerivedType(typeof(SourceTransformedEvent), "SourceTransformedEvent")]
[JsonDerivedType(typeof(SourceTurnReadyEvent), "SourceTurnReadyEvent")]
[JsonDerivedType(typeof(SourceWeatherDamageEvent), "SourceWeatherDamageEvent")]
[JsonDerivedType(typeof(SourceYawnEvent), "SourceYawnEvent")]
public abstract record SourceBattleEvent(long Sequence, int Turn);
public sealed record SourceOrderEvent(long Sequence, int Turn, WildBattleOrderPhase Phase, ImmutableArray<BattlerSlot> Order) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCombatantSnapshot(WildBattleCombatant Body, SourceSlotRegisters Registers);
public sealed record SourcePlayerReplacedEvent(long Sequence, int Turn, BattlerSlot Slot, SourceCombatantSnapshot Previous, SourceCombatantSnapshot Replacement) : SourceBattleEvent(Sequence, Turn);
public sealed record SourcePlayerSwitchedEvent(long Sequence, int Turn, BattlerSlot Slot, SourceCombatantSnapshot Previous, SourceCombatantSnapshot Replacement) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceReplacementRequiredEvent(long Sequence, int Turn, BattlerSlot Slot) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceAbilityActivatedEvent(long Sequence, int Turn, BattlerSlot Side, string Ability) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceAbilityDamageEvent(long Sequence, int Turn, BattlerSlot Holder, BattlerSlot Target, MoveId TriggeringMove, string Ability, int HpBefore, int HpAfter, int Damage) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceAbilityPreventedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, string Ability) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceActionBlockedEvent(long Sequence, int Turn, BattlerSlot Side, MoveId Move, uint Status) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceAttackStringEvent(long Sequence, int Turn, BattlerSlot Actor, MoveId ChosenMove, MoveId CurrentMove) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceBallThrownEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, CaptureBall Ball) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceBindingChangedEvent(long Sequence, int Turn, BattlerSlot Side, int Before, int After, string Reason) : SourceBattleEvent(Sequence, Turn) { public MoveId? Move { get; init; } }
public sealed record SourceBindingDamageEvent(long Sequence, int Turn, BattlerSlot Side, MoveId Move, int HpBefore, int HpAfter, int Damage, int Remaining) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCalledBeatUpMemberEvent(long Sequence, int Turn, BattleSide Side, int PartyIndex, SpeciesId Species) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCalledDelayedEvent(long Sequence, int Turn, BattlerSlot Target, MoveId Move, int Before, int After, string Reason, int? SourcePartyIndex = null) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCalledMoveTransitionEvent(long Sequence, int Turn, SourceMoveInvocation Invocation) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCalledOverlayChangedEvent(long Sequence, int Turn, BattlerSlot Side, MoveId Move, WildBattleCalledOverlay? Before, WildBattleCalledOverlay? After) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCalledPartyChangedEvent(long Sequence, int Turn, BattleSide Side, int PartyIndex, string Reason, WildBattleCalledPartyMember Before, WildBattleCalledPartyMember After) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCalledPartySwitchRequiredEvent(long Sequence, int Turn, SourcePendingSwitch Pending) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCalledPartySwitchedEvent(long Sequence, int Turn, BattlerSlot Side, int OutgoingIndex, int IncomingIndex, WildBattleCalledSwitchKind Kind) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCalledSpikesChangedEvent(long Sequence, int Turn, BattleSide Side, int Before, int After) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCallerSelectionEvent(long Sequence, int Turn, BattlerSlot Actor, MoveId Caller, MoveId? Result, ImmutableArray<int> CandidateSourceMoveIds, ulong RngCursor) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCaptureResultEvent(long Sequence, int Turn, CaptureBall Ball, CaptureOutcome Outcome, CaptureDecisionBranch Branch, CaptureCalculation Calculation, int ChecksPassed, int AnimationCode) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceConfusionChangedEvent(long Sequence, int Turn, BattlerSlot Side, int Before, int After, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceConfusionSelfHitEvent(long Sequence, int Turn, BattlerSlot Side, MoveId SelectedMove, int HpBefore, int HpAfter, int Damage, int RandomPercent) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCriticalHitEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceDefenseCurledEvent(long Sequence, int Turn, BattlerSlot Side) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceDisableChangedEvent(long Sequence, int Turn, BattlerSlot Side, MoveId? Move, int Before, int After, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceDrainEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, int DamageDealt, int HpBefore, int HpAfter, int Restored) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceEffectivenessEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, WildBattleEffectiveness Effectiveness) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceEncoreChangedEvent(long Sequence, int Turn, BattlerSlot Side, int? Slot, int Before, int After, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceEndedEvent(long Sequence, int Turn, WildBattleOutcome Outcome) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceFaintedEvent(long Sequence, int Turn, BattlerSlot Side) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceFlinchEvent(long Sequence, int Turn, BattlerSlot Side, bool Applied) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceFocusEnergyEvent(long Sequence, int Turn, BattlerSlot Side) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceFocusPunchLostEvent(long Sequence, int Turn, BattlerSlot Actor, int PhysicalDamage, int SpecialDamage) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceFocusPunchPreparedEvent(long Sequence, int Turn, BattlerSlot Actor, int ChosenSlot, int PartyIndex) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceForcedOutEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, bool TrainerBattle) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceGrudgePpEvent(long Sequence, int Turn, BattlerSlot Fainted, BattlerSlot Attacker, MoveId Move, int ChosenSlot, int Before) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceHeldItemConsumedEvent(long Sequence, int Turn, BattlerSlot Side, string Item, int HpBefore, int HpAfter, int HpRestored) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceHeldItemKnockedOffEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, string Item) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceHeldItemStolenEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, string Item) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceHpChangedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, int HpBefore, int HpAfter, int Damage, bool Critical, int RandomPercent) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceInfatuationBlockedEvent(long Sequence, int Turn, BattlerSlot Side, BattlerSlot Source, MoveId Move) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceInfatuationChangedEvent(long Sequence, int Turn, BattlerSlot Side, BattlerSlot? Before, BattlerSlot? After, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceIngrainHealEvent(long Sequence, int Turn, BattlerSlot Side, int HpBefore, int HpAfter, int Restored) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceLeechSeedChangedEvent(long Sequence, int Turn, BattlerSlot Side, BattlerSlot? Before, BattlerSlot? After, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceLeechSeedDrainEvent(long Sequence, int Turn, BattlerSlot DrainedSide, BattlerSlot ReceivingSide, int HpBefore, int HpAfter, int ReceiverHpBefore, int ReceiverHpAfter, int Damage, int Restored) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMagicCoatEvent(long Sequence, int Turn, BattlerSlot OriginalActor, BattlerSlot Reflector, MoveId Move) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMagnitudeEvent(long Sequence, int Turn, BattlerSlot Actor, int Magnitude, int Power) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMedicineAppliedEvent(long Sequence, int Turn, ItemId Item, long TargetInstanceId, int PartyIndex, bool ActiveTarget, int HpBefore, int HpAfter, uint StatusBefore, uint StatusAfter) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMoveCopiedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, MoveId CopiedMove, int MoveSlot, bool Permanent, PokemonMoveSlot Replacement) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMoveFailedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMoveMissedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMoveSnatchedEvent(long Sequence, int Turn, BattlerSlot OriginalActor, BattlerSlot Snatcher, MoveId Move, BattlerSlot EffectTarget) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMoveUsedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, int Slot, MoveId Move, int PpBefore, int PpAfter) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMtMoonVolatileEvent(long Sequence, int Turn, BattlerSlot Side, string Volatile, int Before, int After, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMultiHitEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, int PlannedHits, int ActualHits) : SourceBattleEvent(Sequence, Turn);
public sealed record SourcePayDayEvent(long Sequence, int Turn, BattlerSlot Actor, int Before, int After) : SourceBattleEvent(Sequence, Turn);
public sealed record SourcePokeFluteEvent(long Sequence, int Turn, PokeFluteBattlePhase Phase, ImmutableArray<int> PlayerPartyIndices, ImmutableArray<int> OpponentPartyIndices) : SourceBattleEvent(Sequence, Turn);
public sealed record SourcePotionRecoveredEvent(long Sequence, int Turn, BattlerSlot Side, int HpBefore, int HpAfter, int HpRestored) : SourceBattleEvent(Sequence, Turn);
public sealed record SourcePpCommandEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, int PpSlot, MoveId ChosenMove, MoveId CurrentMove, int Before, int After, int CommandCost, ulong RngCursor) : SourceBattleEvent(Sequence, Turn) { public BattlerSlot PpOwner { get; init; } = Actor; }
public sealed record SourceProtectAttemptEvent(long Sequence, int Turn, BattlerSlot Side, MoveId Move, int Before, int After, int Threshold, int Roll, bool NotLastAction, bool Succeeded) : SourceBattleEvent(Sequence, Turn);
public sealed record SourcePursuitInterceptedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot SwitchingSide, int Slot) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceRecoilEvent(long Sequence, int Turn, BattlerSlot Actor, MoveId Move, int HpBefore, int HpAfter, int Damage) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceRecoveredEvent(long Sequence, int Turn, BattlerSlot Side, MoveId Move, int HpBefore, int HpAfter, int Recovered) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceRolloutChangedEvent(long Sequence, int Turn, BattlerSlot Side, int Before, int After, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceRootsChangedEvent(long Sequence, int Turn, BattlerSlot Side, bool Rooted, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceRunAttemptEvent(long Sequence, int Turn, int AttemptsBefore, int AttemptsAfter, int PlayerSpeed, int OpponentSpeed, int? Threshold, int? Roll, bool Escaped) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceSelfDestructEvent(long Sequence, int Turn, BattlerSlot Actor, MoveId Move, int HpBefore) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceSleepTalkEvent(long Sequence, int Turn, BattlerSlot Side, int CalledSlot, MoveId CalledMove) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceSnatchPressurePpEvent(long Sequence, int Turn, BattlerSlot PressuringActor, BattlerSlot Snatcher, int Slot, int Before, int After) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceSnatchReadyEvent(long Sequence, int Turn, BattlerSlot Actor, int ActionOrderIndex) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceSpiteEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, int Slot, int PpBefore, int PpAfter, int RolledReduction) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceStartedEvent(long Sequence, int Turn) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceStatAtMaximumEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, WildBattleStat Stat) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceStatAtMinimumEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, WildBattleStat Stat) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceStatChangedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, WildBattleStat Stat, int Before, int After) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceStatusChangedEvent(long Sequence, int Turn, BattlerSlot Side, uint Before, uint After, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceStatusDamageEvent(long Sequence, int Turn, BattlerSlot Side, uint Status, int HpBefore, int HpAfter, int Damage) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceSubstituteDamagedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, int HpBefore, int HpAfter, int Damage, bool Critical, int RandomPercent) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceTowerBallDodgedEvent(long Sequence, int Turn, CaptureBall Ball) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceTowerGhostActionEvent(long Sequence, int Turn, BattlerSlot Side, string StringId) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceTowerHpEvent(long Sequence, int Turn, BattlerSlot Side, string Effect, int HpBefore, int HpAfter, int Damage) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceTowerVolatileEvent(long Sequence, int Turn, BattlerSlot Side, string Effect, bool Applied, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceTransformedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, SpeciesId OriginalSpecies, SpeciesId AppearanceSpecies, uint AppearancePersonality, uint AppearanceOriginalTrainerId, PokemonInstance BattleView) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceTurnReadyEvent(long Sequence, int Turn, ushort RandomTurnNumber) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceWeatherDamageEvent(long Sequence, int Turn, BattlerSlot Side, string Weather, int HpBefore, int HpAfter, int Damage) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceYawnEvent(long Sequence, int Turn, BattlerSlot Side, int Before, int After, string Reason) : SourceBattleEvent(Sequence, Turn);

/// <summary>Fail-closed migration boundary: unknown future legacy events do not silently lose routing.</summary>
public static class SourceBattleEventAdapter
{
    public static SourceBattleEvent Import(WildBattleEvent value) => value switch {
        WildBattleOrderEvent x => new SourceOrderEvent(x.Sequence, x.Turn, x.Phase, [Slot(x.First), Slot(x.Second)]),
        WildBattlePlayerReplacedEvent x => new SourcePlayerReplacedEvent(x.Sequence, x.Turn, new(0), Snapshot(x.Previous), Snapshot(x.Replacement)),
        WildBattlePlayerSwitchedEvent x => new SourcePlayerSwitchedEvent(x.Sequence, x.Turn, new(0), Snapshot(x.Previous), Snapshot(x.Replacement)),
        WildBattleReplacementRequiredEvent x => new SourceReplacementRequiredEvent(x.Sequence, x.Turn, new(0)),
        WildBattlePermanentWeatherChangedEvent x => new SourcePermanentWeatherChangedEvent(x.Sequence, x.Turn, Slot(x.Side), x.Ability, x.Before, x.After),
        WildBattleAbilityRecoveredEvent x => new SourceAbilityRecoveredEvent(x.Sequence, x.Turn, Slot(x.Side), x.Ability, x.HpBefore, x.HpAfter, x.Recovered),
        WildBattleAbilityActivatedEvent x => new SourceAbilityActivatedEvent(x.Sequence, x.Turn, Slot(x.Side), x.Ability),
        WildBattleAbilityDamageEvent x => new SourceAbilityDamageEvent(x.Sequence, x.Turn, Slot(x.Holder), Slot(x.Target), x.TriggeringMove, x.Ability, x.HpBefore, x.HpAfter, x.Damage),
        WildBattleAbilityPreventedEvent x => new SourceAbilityPreventedEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Move, x.Ability),
        WildBattleActionBlockedEvent x => new SourceActionBlockedEvent(x.Sequence, x.Turn, Slot(x.Side), x.Move, x.Status),
        WildBattleAttackStringEvent x => new SourceAttackStringEvent(x.Sequence, x.Turn, Slot(x.Actor), x.ChosenMove, x.CurrentMove),
        WildBattleBallThrownEvent x => new SourceBallThrownEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Ball),
        WildBattleBindingChangedEvent x => new SourceBindingChangedEvent(x.Sequence, x.Turn, Slot(x.Side), x.Before, x.After, x.Reason) { Move = x.Move },
        WildBattleBindingDamageEvent x => new SourceBindingDamageEvent(x.Sequence, x.Turn, Slot(x.Side), x.Move, x.HpBefore, x.HpAfter, x.Damage, x.Remaining),
        WildBattleCalledBeatUpMemberEvent x => new SourceCalledBeatUpMemberEvent(x.Sequence, x.Turn, PartySide(x.Side), x.PartyIndex, x.Species),
        WildBattleCalledDelayedEvent x => new SourceCalledDelayedEvent(x.Sequence, x.Turn, Slot(x.Target), x.Move, x.Before, x.After, x.Reason, x.SourcePartyIndex),
        WildBattleCalledMoveTransitionEvent x => new SourceCalledMoveTransitionEvent(x.Sequence, x.Turn, SinglesBattleAdapter.ImportInvocation(x.Invocation)),
        WildBattleCalledOverlayChangedEvent x => new SourceCalledOverlayChangedEvent(x.Sequence, x.Turn, Slot(x.Side), x.Move, x.Before, x.After),
        WildBattleCalledPartyChangedEvent x => new SourceCalledPartyChangedEvent(x.Sequence, x.Turn, PartySide(x.Side), x.PartyIndex, x.Reason, x.Before, x.After),
        WildBattleCalledPartySwitchRequiredEvent x => new SourceCalledPartySwitchRequiredEvent(x.Sequence, x.Turn, ImportPending(x.Pending)),
        WildBattleCalledPartySwitchedEvent x => new SourceCalledPartySwitchedEvent(x.Sequence, x.Turn, Slot(x.Side), x.OutgoingIndex, x.IncomingIndex, x.Kind),
        WildBattleCalledSpikesChangedEvent x => new SourceCalledSpikesChangedEvent(x.Sequence, x.Turn, PartySide(x.Side), x.Before, x.After),
        WildBattleCallerSelectionEvent x => new SourceCallerSelectionEvent(x.Sequence, x.Turn, Slot(x.Actor), x.Caller, x.Result, x.CandidateSourceMoveIds, x.RngCursor),
        WildBattleCaptureResultEvent x => new SourceCaptureResultEvent(x.Sequence, x.Turn, x.Ball, x.Outcome, x.Branch, x.Calculation, x.ChecksPassed, x.AnimationCode),
        WildBattleConfusionChangedEvent x => new SourceConfusionChangedEvent(x.Sequence, x.Turn, Slot(x.Side), x.Before, x.After, x.Reason),
        WildBattleConfusionSelfHitEvent x => new SourceConfusionSelfHitEvent(x.Sequence, x.Turn, Slot(x.Side), x.SelectedMove, x.HpBefore, x.HpAfter, x.Damage, x.RandomPercent),
        WildBattleCriticalHitEvent x => new SourceCriticalHitEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target)),
        WildBattleDefenseCurledEvent x => new SourceDefenseCurledEvent(x.Sequence, x.Turn, Slot(x.Side)),
        WildBattleDisableChangedEvent x => new SourceDisableChangedEvent(x.Sequence, x.Turn, Slot(x.Side), x.Move, x.Before, x.After, x.Reason),
        WildBattleDrainEvent x => new SourceDrainEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Move, x.DamageDealt, x.HpBefore, x.HpAfter, x.Restored),
        WildBattleEffectivenessEvent x => new SourceEffectivenessEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Move, x.Effectiveness),
        WildBattleEncoreChangedEvent x => new SourceEncoreChangedEvent(x.Sequence, x.Turn, Slot(x.Side), x.Slot, x.Before, x.After, x.Reason),
        WildBattleEndedEvent x => new SourceEndedEvent(x.Sequence, x.Turn, x.Outcome),
        WildBattleFaintedEvent x => new SourceFaintedEvent(x.Sequence, x.Turn, Slot(x.Side)),
        WildBattleFlinchEvent x => new SourceFlinchEvent(x.Sequence, x.Turn, Slot(x.Side), x.Applied),
        WildBattleFocusEnergyEvent x => new SourceFocusEnergyEvent(x.Sequence, x.Turn, Slot(x.Side)),
        WildBattleFocusPunchLostEvent x => new SourceFocusPunchLostEvent(x.Sequence, x.Turn, Slot(x.Actor), x.PhysicalDamage, x.SpecialDamage),
        WildBattleFocusPunchPreparedEvent x => new SourceFocusPunchPreparedEvent(x.Sequence, x.Turn, Slot(x.Actor), x.ChosenSlot, x.PartyIndex),
        WildBattleForcedOutEvent x => new SourceForcedOutEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Move, x.TrainerBattle),
        WildBattleGrudgePpEvent x => new SourceGrudgePpEvent(x.Sequence, x.Turn, Slot(x.Fainted), Slot(x.Attacker), x.Move, x.ChosenSlot, x.Before),
        WildBattleHeldItemConsumedEvent x => new SourceHeldItemConsumedEvent(x.Sequence, x.Turn, Slot(x.Side), x.Item, x.HpBefore, x.HpAfter, x.HpRestored),
        WildBattleHeldItemKnockedOffEvent x => new SourceHeldItemKnockedOffEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Move, x.Item),
        WildBattleHeldItemStolenEvent x => new SourceHeldItemStolenEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Move, x.Item),
        WildBattleHpChangedEvent x => new SourceHpChangedEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Move, x.HpBefore, x.HpAfter, x.Damage, x.Critical, x.RandomPercent),
        WildBattleInfatuationBlockedEvent x => new SourceInfatuationBlockedEvent(x.Sequence, x.Turn, Slot(x.Side), Slot(x.Source), x.Move),
        WildBattleInfatuationChangedEvent x => new SourceInfatuationChangedEvent(x.Sequence, x.Turn, Slot(x.Side), Slot(x.Before), Slot(x.After), x.Reason),
        WildBattleIngrainHealEvent x => new SourceIngrainHealEvent(x.Sequence, x.Turn, Slot(x.Side), x.HpBefore, x.HpAfter, x.Restored),
        WildBattleLeechSeedChangedEvent x => new SourceLeechSeedChangedEvent(x.Sequence, x.Turn, Slot(x.Side), Slot(x.Before), Slot(x.After), x.Reason),
        WildBattleLeechSeedDrainEvent x => new SourceLeechSeedDrainEvent(x.Sequence, x.Turn, Slot(x.DrainedSide), Slot(x.ReceivingSide), x.HpBefore, x.HpAfter, x.ReceiverHpBefore, x.ReceiverHpAfter, x.Damage, x.Restored),
        WildBattleMagicCoatEvent x => new SourceMagicCoatEvent(x.Sequence, x.Turn, Slot(x.OriginalActor), Slot(x.Reflector), x.Move),
        WildBattleMagnitudeEvent x => new SourceMagnitudeEvent(x.Sequence, x.Turn, Slot(x.Actor), x.Magnitude, x.Power),
        WildBattleMedicineAppliedEvent x => new SourceMedicineAppliedEvent(x.Sequence, x.Turn, x.Item, x.TargetInstanceId, x.PartyIndex, x.ActiveTarget, x.HpBefore, x.HpAfter, x.StatusBefore, x.StatusAfter),
        WildBattleMoveCopiedEvent x => new SourceMoveCopiedEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Move, x.CopiedMove, x.MoveSlot, x.Permanent, x.Replacement),
        WildBattleMoveFailedEvent x => new SourceMoveFailedEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Move, x.Reason),
        WildBattleMoveMissedEvent x => new SourceMoveMissedEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Move),
        WildBattleMoveSnatchedEvent x => new SourceMoveSnatchedEvent(x.Sequence, x.Turn, Slot(x.OriginalActor), Slot(x.Snatcher), x.Move, Slot(x.EffectTarget)),
        WildBattleMoveUsedEvent x => new SourceMoveUsedEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Slot, x.Move, x.PpBefore, x.PpAfter),
        WildBattleMtMoonVolatileEvent x => new SourceMtMoonVolatileEvent(x.Sequence, x.Turn, Slot(x.Side), x.Volatile, x.Before, x.After, x.Reason),
        WildBattleMultiHitEvent x => new SourceMultiHitEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Move, x.PlannedHits, x.ActualHits),
        WildBattlePayDayEvent x => new SourcePayDayEvent(x.Sequence, x.Turn, Slot(x.Actor), x.Before, x.After),
        WildBattlePokeFluteEvent x => new SourcePokeFluteEvent(x.Sequence, x.Turn, x.Phase, x.PlayerPartyIndices, x.OpponentPartyIndices),
        WildBattlePotionRecoveredEvent x => new SourcePotionRecoveredEvent(x.Sequence, x.Turn, Slot(x.Side), x.HpBefore, x.HpAfter, x.HpRestored),
        WildBattlePpCommandEvent x => new SourcePpCommandEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.PpSlot, x.ChosenMove, x.CurrentMove, x.Before, x.After, x.CommandCost, x.RngCursor) { PpOwner = Slot(x.PpOwner) },
        WildBattleProtectAttemptEvent x => new SourceProtectAttemptEvent(x.Sequence, x.Turn, Slot(x.Side), x.Move, x.Before, x.After, x.Threshold, x.Roll, x.NotLastAction, x.Succeeded),
        WildBattlePursuitInterceptedEvent x => new SourcePursuitInterceptedEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.SwitchingSide), x.Slot),
        WildBattleRecoilEvent x => new SourceRecoilEvent(x.Sequence, x.Turn, Slot(x.Actor), x.Move, x.HpBefore, x.HpAfter, x.Damage),
        WildBattleRecoveredEvent x => new SourceRecoveredEvent(x.Sequence, x.Turn, Slot(x.Side), x.Move, x.HpBefore, x.HpAfter, x.Recovered),
        WildBattleRolloutChangedEvent x => new SourceRolloutChangedEvent(x.Sequence, x.Turn, Slot(x.Side), x.Before, x.After, x.Reason),
        WildBattleRootsChangedEvent x => new SourceRootsChangedEvent(x.Sequence, x.Turn, Slot(x.Side), x.Rooted, x.Reason),
        WildBattleRunAttemptEvent x => new SourceRunAttemptEvent(x.Sequence, x.Turn, x.AttemptsBefore, x.AttemptsAfter, x.PlayerSpeed, x.OpponentSpeed, x.Threshold, x.Roll, x.Escaped),
        WildBattleSelfDestructEvent x => new SourceSelfDestructEvent(x.Sequence, x.Turn, Slot(x.Actor), x.Move, x.HpBefore),
        WildBattleSleepTalkEvent x => new SourceSleepTalkEvent(x.Sequence, x.Turn, Slot(x.Side), x.CalledSlot, x.CalledMove),
        WildBattleSnatchPressurePpEvent x => new SourceSnatchPressurePpEvent(x.Sequence, x.Turn, Slot(x.PressuringActor), Slot(x.Snatcher), x.Slot, x.Before, x.After),
        WildBattleSnatchReadyEvent x => new SourceSnatchReadyEvent(x.Sequence, x.Turn, Slot(x.Actor), x.ActionOrderIndex),
        WildBattleSpiteEvent x => new SourceSpiteEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Move, x.Slot, x.PpBefore, x.PpAfter, x.RolledReduction),
        WildBattleStartedEvent x => new SourceStartedEvent(x.Sequence, x.Turn),
        WildBattleStatAtMaximumEvent x => new SourceStatAtMaximumEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Stat),
        WildBattleStatAtMinimumEvent x => new SourceStatAtMinimumEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Stat),
        WildBattleStatChangedEvent x => new SourceStatChangedEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Stat, x.Before, x.After),
        WildBattleStatusChangedEvent x => new SourceStatusChangedEvent(x.Sequence, x.Turn, Slot(x.Side), x.Before, x.After, x.Reason),
        WildBattleStatusDamageEvent x => new SourceStatusDamageEvent(x.Sequence, x.Turn, Slot(x.Side), x.Status, x.HpBefore, x.HpAfter, x.Damage),
        WildBattleSubstituteDamagedEvent x => new SourceSubstituteDamagedEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.Move, x.HpBefore, x.HpAfter, x.Damage, x.Critical, x.RandomPercent),
        WildBattleTowerBallDodgedEvent x => new SourceTowerBallDodgedEvent(x.Sequence, x.Turn, x.Ball),
        WildBattleTowerGhostActionEvent x => new SourceTowerGhostActionEvent(x.Sequence, x.Turn, Slot(x.Side), x.StringId),
        WildBattleTowerHpEvent x => new SourceTowerHpEvent(x.Sequence, x.Turn, Slot(x.Side), x.Effect, x.HpBefore, x.HpAfter, x.Damage),
        WildBattleTowerVolatileEvent x => new SourceTowerVolatileEvent(x.Sequence, x.Turn, Slot(x.Side), x.Effect, x.Applied, x.Reason),
        WildBattleTransformedEvent x => new SourceTransformedEvent(x.Sequence, x.Turn, Slot(x.Actor), Slot(x.Target), x.OriginalSpecies, x.AppearanceSpecies, x.AppearancePersonality, x.AppearanceOriginalTrainerId, x.BattleView),
        WildBattleTurnReadyEvent x => new SourceTurnReadyEvent(x.Sequence, x.Turn, x.RandomTurnNumber),
        WildBattleWeatherDamageEvent x => new SourceWeatherDamageEvent(x.Sequence, x.Turn, Slot(x.Side), x.Weather, x.HpBefore, x.HpAfter, x.Damage),
        WildBattleYawnEvent x => new SourceYawnEvent(x.Sequence, x.Turn, Slot(x.Side), x.Before, x.After, x.Reason),
        _ => throw new NotSupportedException("Unmigrated singles event: " + value.GetType().Name)
    };
    public static WildBattleEvent Export(SourceBattleEvent value) => value switch {
        SourceOrderEvent x when x.Order.Length == 2 => new WildBattleOrderEvent(x.Sequence, x.Turn, x.Phase, Side(x.Order[0]), Side(x.Order[1])),
        SourcePlayerReplacedEvent x when x.Slot.Value == 0 => new WildBattlePlayerReplacedEvent(x.Sequence, x.Turn, Body(x.Previous), Body(x.Replacement)),
        SourcePlayerSwitchedEvent x when x.Slot.Value == 0 => new WildBattlePlayerSwitchedEvent(x.Sequence, x.Turn, Body(x.Previous), Body(x.Replacement)),
        SourceReplacementRequiredEvent x when x.Slot.Value == 0 => new WildBattleReplacementRequiredEvent(x.Sequence, x.Turn),
        SourcePermanentWeatherChangedEvent x => new WildBattlePermanentWeatherChangedEvent(x.Sequence, x.Turn, Side(x.Side), x.Ability, x.Before, x.After),
        SourceAbilityRecoveredEvent x => new WildBattleAbilityRecoveredEvent(x.Sequence, x.Turn, Side(x.Side), x.Ability, x.HpBefore, x.HpAfter, x.Recovered),
        SourceAbilityActivatedEvent x => new WildBattleAbilityActivatedEvent(x.Sequence, x.Turn, Side(x.Side), x.Ability),
        SourceAbilityDamageEvent x => new WildBattleAbilityDamageEvent(x.Sequence, x.Turn, Side(x.Holder), Side(x.Target), x.TriggeringMove, x.Ability, x.HpBefore, x.HpAfter, x.Damage),
        SourceAbilityPreventedEvent x => new WildBattleAbilityPreventedEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Move, x.Ability),
        SourceActionBlockedEvent x => new WildBattleActionBlockedEvent(x.Sequence, x.Turn, Side(x.Side), x.Move, x.Status),
        SourceAttackStringEvent x => new WildBattleAttackStringEvent(x.Sequence, x.Turn, Side(x.Actor), x.ChosenMove, x.CurrentMove),
        SourceBallThrownEvent x => new WildBattleBallThrownEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Ball),
        SourceBindingChangedEvent x => new WildBattleBindingChangedEvent(x.Sequence, x.Turn, Side(x.Side), x.Before, x.After, x.Reason) { Move = x.Move },
        SourceBindingDamageEvent x => new WildBattleBindingDamageEvent(x.Sequence, x.Turn, Side(x.Side), x.Move, x.HpBefore, x.HpAfter, x.Damage, x.Remaining),
        SourceCalledBeatUpMemberEvent x => new WildBattleCalledBeatUpMemberEvent(x.Sequence, x.Turn, LegacyPartySide(x.Side), x.PartyIndex, x.Species),
        SourceCalledDelayedEvent x => new WildBattleCalledDelayedEvent(x.Sequence, x.Turn, Side(x.Target), x.Move, x.Before, x.After, x.Reason, x.SourcePartyIndex),
        SourceCalledMoveTransitionEvent x => new WildBattleCalledMoveTransitionEvent(x.Sequence, x.Turn, SinglesBattleAdapter.ExportInvocation(x.Invocation)),
        SourceCalledOverlayChangedEvent x => new WildBattleCalledOverlayChangedEvent(x.Sequence, x.Turn, Side(x.Side), x.Move, x.Before, x.After),
        SourceCalledPartyChangedEvent x => new WildBattleCalledPartyChangedEvent(x.Sequence, x.Turn, LegacyPartySide(x.Side), x.PartyIndex, x.Reason, x.Before, x.After),
        SourceCalledPartySwitchRequiredEvent x => new WildBattleCalledPartySwitchRequiredEvent(x.Sequence, x.Turn, ExportPending(x.Pending)),
        SourceCalledPartySwitchedEvent x => new WildBattleCalledPartySwitchedEvent(x.Sequence, x.Turn, Side(x.Side), x.OutgoingIndex, x.IncomingIndex, x.Kind),
        SourceCalledSpikesChangedEvent x => new WildBattleCalledSpikesChangedEvent(x.Sequence, x.Turn, LegacyPartySide(x.Side), x.Before, x.After),
        SourceCallerSelectionEvent x => new WildBattleCallerSelectionEvent(x.Sequence, x.Turn, Side(x.Actor), x.Caller, x.Result, x.CandidateSourceMoveIds, x.RngCursor),
        SourceCaptureResultEvent x => new WildBattleCaptureResultEvent(x.Sequence, x.Turn, x.Ball, x.Outcome, x.Branch, x.Calculation, x.ChecksPassed, x.AnimationCode),
        SourceConfusionChangedEvent x => new WildBattleConfusionChangedEvent(x.Sequence, x.Turn, Side(x.Side), x.Before, x.After, x.Reason),
        SourceConfusionSelfHitEvent x => new WildBattleConfusionSelfHitEvent(x.Sequence, x.Turn, Side(x.Side), x.SelectedMove, x.HpBefore, x.HpAfter, x.Damage, x.RandomPercent),
        SourceCriticalHitEvent x => new WildBattleCriticalHitEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target)),
        SourceDefenseCurledEvent x => new WildBattleDefenseCurledEvent(x.Sequence, x.Turn, Side(x.Side)),
        SourceDisableChangedEvent x => new WildBattleDisableChangedEvent(x.Sequence, x.Turn, Side(x.Side), x.Move, x.Before, x.After, x.Reason),
        SourceDrainEvent x => new WildBattleDrainEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Move, x.DamageDealt, x.HpBefore, x.HpAfter, x.Restored),
        SourceEffectivenessEvent x => new WildBattleEffectivenessEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Move, x.Effectiveness),
        SourceEncoreChangedEvent x => new WildBattleEncoreChangedEvent(x.Sequence, x.Turn, Side(x.Side), x.Slot, x.Before, x.After, x.Reason),
        SourceEndedEvent x => new WildBattleEndedEvent(x.Sequence, x.Turn, x.Outcome),
        SourceFaintedEvent x => new WildBattleFaintedEvent(x.Sequence, x.Turn, Side(x.Side)),
        SourceFlinchEvent x => new WildBattleFlinchEvent(x.Sequence, x.Turn, Side(x.Side), x.Applied),
        SourceFocusEnergyEvent x => new WildBattleFocusEnergyEvent(x.Sequence, x.Turn, Side(x.Side)),
        SourceFocusPunchLostEvent x => new WildBattleFocusPunchLostEvent(x.Sequence, x.Turn, Side(x.Actor), x.PhysicalDamage, x.SpecialDamage),
        SourceFocusPunchPreparedEvent x => new WildBattleFocusPunchPreparedEvent(x.Sequence, x.Turn, Side(x.Actor), x.ChosenSlot, x.PartyIndex),
        SourceForcedOutEvent x => new WildBattleForcedOutEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Move, x.TrainerBattle),
        SourceGrudgePpEvent x => new WildBattleGrudgePpEvent(x.Sequence, x.Turn, Side(x.Fainted), Side(x.Attacker), x.Move, x.ChosenSlot, x.Before),
        SourceHeldItemConsumedEvent x => new WildBattleHeldItemConsumedEvent(x.Sequence, x.Turn, Side(x.Side), x.Item, x.HpBefore, x.HpAfter, x.HpRestored),
        SourceHeldItemKnockedOffEvent x => new WildBattleHeldItemKnockedOffEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Move, x.Item),
        SourceHeldItemStolenEvent x => new WildBattleHeldItemStolenEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Move, x.Item),
        SourceHpChangedEvent x => new WildBattleHpChangedEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Move, x.HpBefore, x.HpAfter, x.Damage, x.Critical, x.RandomPercent),
        SourceInfatuationBlockedEvent x => new WildBattleInfatuationBlockedEvent(x.Sequence, x.Turn, Side(x.Side), Side(x.Source), x.Move),
        SourceInfatuationChangedEvent x => new WildBattleInfatuationChangedEvent(x.Sequence, x.Turn, Side(x.Side), Side(x.Before), Side(x.After), x.Reason),
        SourceIngrainHealEvent x => new WildBattleIngrainHealEvent(x.Sequence, x.Turn, Side(x.Side), x.HpBefore, x.HpAfter, x.Restored),
        SourceLeechSeedChangedEvent x => new WildBattleLeechSeedChangedEvent(x.Sequence, x.Turn, Side(x.Side), Side(x.Before), Side(x.After), x.Reason),
        SourceLeechSeedDrainEvent x => new WildBattleLeechSeedDrainEvent(x.Sequence, x.Turn, Side(x.DrainedSide), Side(x.ReceivingSide), x.HpBefore, x.HpAfter, x.ReceiverHpBefore, x.ReceiverHpAfter, x.Damage, x.Restored),
        SourceMagicCoatEvent x => new WildBattleMagicCoatEvent(x.Sequence, x.Turn, Side(x.OriginalActor), Side(x.Reflector), x.Move),
        SourceMagnitudeEvent x => new WildBattleMagnitudeEvent(x.Sequence, x.Turn, Side(x.Actor), x.Magnitude, x.Power),
        SourceMedicineAppliedEvent x => new WildBattleMedicineAppliedEvent(x.Sequence, x.Turn, x.Item, x.TargetInstanceId, x.PartyIndex, x.ActiveTarget, x.HpBefore, x.HpAfter, x.StatusBefore, x.StatusAfter),
        SourceMoveCopiedEvent x => new WildBattleMoveCopiedEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Move, x.CopiedMove, x.MoveSlot, x.Permanent, x.Replacement),
        SourceMoveFailedEvent x => new WildBattleMoveFailedEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Move, x.Reason),
        SourceMoveMissedEvent x => new WildBattleMoveMissedEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Move),
        SourceMoveSnatchedEvent x => new WildBattleMoveSnatchedEvent(x.Sequence, x.Turn, Side(x.OriginalActor), Side(x.Snatcher), x.Move, Side(x.EffectTarget)),
        SourceMoveUsedEvent x => new WildBattleMoveUsedEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Slot, x.Move, x.PpBefore, x.PpAfter),
        SourceMtMoonVolatileEvent x => new WildBattleMtMoonVolatileEvent(x.Sequence, x.Turn, Side(x.Side), x.Volatile, x.Before, x.After, x.Reason),
        SourceMultiHitEvent x => new WildBattleMultiHitEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Move, x.PlannedHits, x.ActualHits),
        SourcePayDayEvent x => new WildBattlePayDayEvent(x.Sequence, x.Turn, Side(x.Actor), x.Before, x.After),
        SourcePokeFluteEvent x => new WildBattlePokeFluteEvent(x.Sequence, x.Turn, x.Phase, x.PlayerPartyIndices, x.OpponentPartyIndices),
        SourcePotionRecoveredEvent x => new WildBattlePotionRecoveredEvent(x.Sequence, x.Turn, Side(x.Side), x.HpBefore, x.HpAfter, x.HpRestored),
        SourcePpCommandEvent x => new WildBattlePpCommandEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.PpSlot, x.ChosenMove, x.CurrentMove, x.Before, x.After, x.CommandCost, x.RngCursor) { PpOwner = Side(x.PpOwner) },
        SourceProtectAttemptEvent x => new WildBattleProtectAttemptEvent(x.Sequence, x.Turn, Side(x.Side), x.Move, x.Before, x.After, x.Threshold, x.Roll, x.NotLastAction, x.Succeeded),
        SourcePursuitInterceptedEvent x => new WildBattlePursuitInterceptedEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.SwitchingSide), x.Slot),
        SourceRecoilEvent x => new WildBattleRecoilEvent(x.Sequence, x.Turn, Side(x.Actor), x.Move, x.HpBefore, x.HpAfter, x.Damage),
        SourceRecoveredEvent x => new WildBattleRecoveredEvent(x.Sequence, x.Turn, Side(x.Side), x.Move, x.HpBefore, x.HpAfter, x.Recovered),
        SourceRolloutChangedEvent x => new WildBattleRolloutChangedEvent(x.Sequence, x.Turn, Side(x.Side), x.Before, x.After, x.Reason),
        SourceRootsChangedEvent x => new WildBattleRootsChangedEvent(x.Sequence, x.Turn, Side(x.Side), x.Rooted, x.Reason),
        SourceRunAttemptEvent x => new WildBattleRunAttemptEvent(x.Sequence, x.Turn, x.AttemptsBefore, x.AttemptsAfter, x.PlayerSpeed, x.OpponentSpeed, x.Threshold, x.Roll, x.Escaped),
        SourceSelfDestructEvent x => new WildBattleSelfDestructEvent(x.Sequence, x.Turn, Side(x.Actor), x.Move, x.HpBefore),
        SourceSleepTalkEvent x => new WildBattleSleepTalkEvent(x.Sequence, x.Turn, Side(x.Side), x.CalledSlot, x.CalledMove),
        SourceSnatchPressurePpEvent x => new WildBattleSnatchPressurePpEvent(x.Sequence, x.Turn, Side(x.PressuringActor), Side(x.Snatcher), x.Slot, x.Before, x.After),
        SourceSnatchReadyEvent x => new WildBattleSnatchReadyEvent(x.Sequence, x.Turn, Side(x.Actor), x.ActionOrderIndex),
        SourceSpiteEvent x => new WildBattleSpiteEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Move, x.Slot, x.PpBefore, x.PpAfter, x.RolledReduction),
        SourceStartedEvent x => new WildBattleStartedEvent(x.Sequence, x.Turn),
        SourceStatAtMaximumEvent x => new WildBattleStatAtMaximumEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Stat),
        SourceStatAtMinimumEvent x => new WildBattleStatAtMinimumEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Stat),
        SourceStatChangedEvent x => new WildBattleStatChangedEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Stat, x.Before, x.After),
        SourceStatusChangedEvent x => new WildBattleStatusChangedEvent(x.Sequence, x.Turn, Side(x.Side), x.Before, x.After, x.Reason),
        SourceStatusDamageEvent x => new WildBattleStatusDamageEvent(x.Sequence, x.Turn, Side(x.Side), x.Status, x.HpBefore, x.HpAfter, x.Damage),
        SourceSubstituteDamagedEvent x => new WildBattleSubstituteDamagedEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.Move, x.HpBefore, x.HpAfter, x.Damage, x.Critical, x.RandomPercent),
        SourceTowerBallDodgedEvent x => new WildBattleTowerBallDodgedEvent(x.Sequence, x.Turn, x.Ball),
        SourceTowerGhostActionEvent x => new WildBattleTowerGhostActionEvent(x.Sequence, x.Turn, Side(x.Side), x.StringId),
        SourceTowerHpEvent x => new WildBattleTowerHpEvent(x.Sequence, x.Turn, Side(x.Side), x.Effect, x.HpBefore, x.HpAfter, x.Damage),
        SourceTowerVolatileEvent x => new WildBattleTowerVolatileEvent(x.Sequence, x.Turn, Side(x.Side), x.Effect, x.Applied, x.Reason),
        SourceTransformedEvent x => new WildBattleTransformedEvent(x.Sequence, x.Turn, Side(x.Actor), Side(x.Target), x.OriginalSpecies, x.AppearanceSpecies, x.AppearancePersonality, x.AppearanceOriginalTrainerId, x.BattleView),
        SourceTurnReadyEvent x => new WildBattleTurnReadyEvent(x.Sequence, x.Turn, x.RandomTurnNumber),
        SourceWeatherDamageEvent x => new WildBattleWeatherDamageEvent(x.Sequence, x.Turn, Side(x.Side), x.Weather, x.HpBefore, x.HpAfter, x.Damage),
        SourceYawnEvent x => new WildBattleYawnEvent(x.Sequence, x.Turn, Side(x.Side), x.Before, x.After, x.Reason),
        _ => throw new NotSupportedException("Canonical event has no lossless singles representation: " + value.GetType().Name)
    };
    private static BattlerSlot Slot(WildBattleSide side) => SinglesBattleAdapter.Slot(side);
    private static BattlerSlot? Slot(WildBattleSide? side) => side is { } s ? Slot(s) : null;
    private static WildBattleSide Side(BattlerSlot slot) => SinglesBattleAdapter.Side(slot);
    private static WildBattleSide? Side(BattlerSlot? slot) => slot is { } s ? Side(s) : null;
    private static BattleSide PartySide(WildBattleSide side) => (BattleSide)(int)Side(Slot(side));
    private static WildBattleSide LegacyPartySide(BattleSide side) => Enum.IsDefined(side) ? (WildBattleSide)(int)side : throw new ArgumentOutOfRangeException(nameof(side));
    private static SourcePendingSwitch ImportPending(WildBattleCalledPendingSwitch x) => new(Slot(x.Side), x.Kind, x.LegalPartyIndices,
        x.Invocation is { } invocation ? SinglesBattleAdapter.ImportInvocation(invocation) : null);
    private static WildBattleCalledPendingSwitch ExportPending(SourcePendingSwitch x) => new(Side(x.Slot), x.Kind, x.LegalPartyIndices,
        x.Invocation is { } invocation ? SinglesBattleAdapter.ExportInvocation(invocation) : null);
    private static SourceCombatantSnapshot Snapshot(WildBattleCombatant body)
    {
        var cell = SinglesBattleAdapter.ImportSlot(new(0), new(BattleSide.Player, 0), 1, body);
        return new(cell.Body!, cell.Registers);
    }
    private static WildBattleCombatant Body(SourceCombatantSnapshot snapshot) => SinglesBattleAdapter.ExportBody(snapshot.Body, snapshot.Registers);
}

/// <summary>Canonical publication hook. No move mechanics or damage arithmetic is duplicated here.</summary>
public static class SourceBattleEventHooks
{
    public static (SourceBattleFrame State, SourceBattleEvent Event) Publish(SourceBattleFrame frame,
        Func<long, int, SourceBattleEvent> create)
    {
        if (SourceBattleIdentityRules.Validate(frame) is { } fault) throw new ArgumentException(fault, nameof(frame));
        var value = create(checked(frame.EventSequence + 1), frame.Turn);
        if (value.Sequence != frame.EventSequence + 1 || value.Turn != frame.Turn) throw new ArgumentException("source_event_sequence", nameof(create));
        if (ValidateRouting(frame, value) is { } error) throw new ArgumentException(error, nameof(create));
        return (frame with { EventSequence = value.Sequence }, value);
    }
    public static string? ValidateRouting(SourceBattleFrame frame, SourceBattleEvent value)
    {
        if (value is SourceOrderEvent order && (order.Order.IsDefault || order.Order.Length != frame.BattlerCount ||
            order.Order.Distinct().Count() != frame.BattlerCount)) return "source_event_order_shape";
        if (value is SourceCalledSpikesChangedEvent spikes && !Enum.IsDefined(spikes.Side) ||
            value is SourceCalledPartyChangedEvent party && (!Enum.IsDefined(party.Side) || party.PartyIndex < 0 || party.PartyIndex >= frame.Party(party.Side).Members.Length) ||
            value is SourceCalledBeatUpMemberEvent contributor && (!Enum.IsDefined(contributor.Side) || contributor.PartyIndex < 0 || contributor.PartyIndex >= frame.Party(contributor.Side).Members.Length))
            return "source_event_party_identity";
        static bool PlayerKey(SourceBattleFrame f, PartyMemberKey key) => key.Side == BattleSide.Player && key.Index < f.Party(BattleSide.Player).Members.Length;
        static bool DefeatKey(SourceBattleFrame f, OccupantToken token) => token.Member.Side == BattleSide.Opponent &&
            token.Slot.Side == BattleSide.Opponent && f.IsCurrent(token) && f.Get(token.Slot).Body!.Pokemon.CurrentHp == 0;
        if (value is SourceRewardAppliedEvent reward && (!PlayerKey(frame, reward.Recipient) || !DefeatKey(frame, reward.Defeated)) ||
            value is SourceRewardLearningEvent learning && (!PlayerKey(frame, learning.Recipient) || !Enum.IsDefined(learning.Outcome)) ||
            value is SourceOpponentRewardCompletedEvent completed && !DefeatKey(frame, completed.Defeated))
            return "source_event_reward_identity";
        IEnumerable<BattlerSlot?> Slots() => value switch {
            SourceRewardAppliedEvent x => [x.Defeated.Slot],
            SourceRewardLearningEvent x => [],
            SourceOpponentRewardCompletedEvent x => [x.Defeated.Slot],
            SourcePermanentWeatherChangedEvent x => [x.Side],
            SourceAbilityRecoveredEvent x => [x.Side],
            SourceAbilityActivatedEvent x => [x.Side],
            SourceAbilityDamageEvent x => [x.Holder, x.Target],
            SourceAbilityPreventedEvent x => [x.Actor, x.Target],
            SourceActionBlockedEvent x => [x.Side],
            SourceAttackStringEvent x => [x.Actor],
            SourceBallThrownEvent x => [x.Actor, x.Target],
            SourceBindingChangedEvent x => [x.Side],
            SourceBindingDamageEvent x => [x.Side],
            SourceCalledBeatUpMemberEvent x => [],
            SourceCalledDelayedEvent x => [x.Target],
            SourceCalledMoveTransitionEvent x => [x.Invocation.Actor, x.Invocation.Target, x.Invocation.PpOwner],
            SourceCalledOverlayChangedEvent x => [x.Side],
            SourceCalledPartyChangedEvent x => [],
            SourceCalledPartySwitchRequiredEvent x => [x.Pending.Slot, x.Pending.Invocation?.Actor, x.Pending.Invocation?.Target, x.Pending.Invocation?.PpOwner],
            SourceCalledPartySwitchedEvent x => [x.Side],
            SourceCalledSpikesChangedEvent x => [],
            SourceCallerSelectionEvent x => [x.Actor],
            SourceCaptureResultEvent x => [],
            SourceConfusionChangedEvent x => [x.Side],
            SourceConfusionSelfHitEvent x => [x.Side],
            SourceCriticalHitEvent x => [x.Actor, x.Target],
            SourceDefenseCurledEvent x => [x.Side],
            SourceDisableChangedEvent x => [x.Side],
            SourceDrainEvent x => [x.Actor, x.Target],
            SourceEffectivenessEvent x => [x.Actor, x.Target],
            SourceEncoreChangedEvent x => [x.Side],
            SourceEndedEvent x => [],
            SourceFaintedEvent x => [x.Side],
            SourceFlinchEvent x => [x.Side],
            SourceFocusEnergyEvent x => [x.Side],
            SourceFocusPunchLostEvent x => [x.Actor],
            SourceFocusPunchPreparedEvent x => [x.Actor],
            SourceForcedOutEvent x => [x.Actor, x.Target],
            SourceGrudgePpEvent x => [x.Fainted, x.Attacker],
            SourceHeldItemConsumedEvent x => [x.Side],
            SourceHeldItemKnockedOffEvent x => [x.Actor, x.Target],
            SourceHeldItemStolenEvent x => [x.Actor, x.Target],
            SourceHpChangedEvent x => [x.Actor, x.Target],
            SourceInfatuationBlockedEvent x => [x.Side, x.Source],
            SourceInfatuationChangedEvent x => [x.Side, x.Before, x.After],
            SourceIngrainHealEvent x => [x.Side],
            SourceLeechSeedChangedEvent x => [x.Side, x.Before, x.After],
            SourceLeechSeedDrainEvent x => [x.DrainedSide, x.ReceivingSide],
            SourceMagicCoatEvent x => [x.OriginalActor, x.Reflector],
            SourceMagnitudeEvent x => [x.Actor],
            SourceMedicineAppliedEvent x => [],
            SourceMoveCopiedEvent x => [x.Actor, x.Target],
            SourceMoveFailedEvent x => [x.Actor, x.Target],
            SourceMoveMissedEvent x => [x.Actor, x.Target],
            SourceMoveSnatchedEvent x => [x.OriginalActor, x.Snatcher, x.EffectTarget],
            SourceMoveUsedEvent x => [x.Actor, x.Target],
            SourceMtMoonVolatileEvent x => [x.Side],
            SourceMultiHitEvent x => [x.Actor, x.Target],
            SourcePayDayEvent x => [x.Actor],
            SourcePokeFluteEvent x => [],
            SourcePotionRecoveredEvent x => [x.Side],
            SourcePpCommandEvent x => [x.Actor, x.Target, x.PpOwner],
            SourceProtectAttemptEvent x => [x.Side],
            SourcePursuitInterceptedEvent x => [x.Actor, x.SwitchingSide],
            SourceRecoilEvent x => [x.Actor],
            SourceRecoveredEvent x => [x.Side],
            SourceRolloutChangedEvent x => [x.Side],
            SourceRootsChangedEvent x => [x.Side],
            SourceRunAttemptEvent x => [],
            SourceSelfDestructEvent x => [x.Actor],
            SourceSleepTalkEvent x => [x.Side],
            SourceSnatchPressurePpEvent x => [x.PressuringActor, x.Snatcher],
            SourceSnatchReadyEvent x => [x.Actor],
            SourceSpiteEvent x => [x.Actor, x.Target],
            SourceStartedEvent x => [],
            SourceStatAtMaximumEvent x => [x.Actor, x.Target],
            SourceStatAtMinimumEvent x => [x.Actor, x.Target],
            SourceStatChangedEvent x => [x.Actor, x.Target],
            SourceStatusChangedEvent x => [x.Side],
            SourceStatusDamageEvent x => [x.Side],
            SourceSubstituteDamagedEvent x => [x.Actor, x.Target],
            SourceTowerBallDodgedEvent x => [],
            SourceTowerGhostActionEvent x => [x.Side],
            SourceTowerHpEvent x => [x.Side],
            SourceTowerVolatileEvent x => [x.Side],
            SourceTransformedEvent x => [x.Actor, x.Target],
            SourceTurnReadyEvent x => [],
            SourceWeatherDamageEvent x => [x.Side],
            SourceYawnEvent x => [x.Side],
            SourceOrderEvent x => x.Order.Select(s => (BattlerSlot?)s),
            SourcePlayerReplacedEvent x => [x.Slot],
            SourcePlayerSwitchedEvent x => [x.Slot],
            SourceReplacementRequiredEvent x => [x.Slot],
            _ => throw new NotSupportedException("Unknown canonical event routing: " + value.GetType().Name)
        };
        return Slots().Any(slot => slot is { } s && s.Value >= frame.BattlerCount) ? "source_event_slot_outside_layout" : null;
    }
}

public sealed record SourcePermanentWeatherChangedEvent(long Sequence, int Turn, BattlerSlot Side, string Ability,
    SourcePermanentWeatherKind? Before, SourcePermanentWeatherKind After) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceAbilityRecoveredEvent(long Sequence, int Turn, BattlerSlot Side, string Ability,
    int HpBefore, int HpAfter, int Recovered) : SourceBattleEvent(Sequence, Turn);

public sealed record SourceRewardAppliedEvent(long Sequence, int Turn, OccupantToken Defeated, PartyMemberKey Recipient, PokemonRewardResult Reward) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceRewardLearningEvent(long Sequence, int Turn, PartyMemberKey Recipient, WildPendingMove Candidate, SourceRewardLearningOutcome Outcome, int? MoveSlot) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceOpponentRewardCompletedEvent(long Sequence, int Turn, OccupantToken Defeated) : SourceBattleEvent(Sequence, Turn);
