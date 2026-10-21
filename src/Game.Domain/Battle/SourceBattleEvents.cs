using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Capture;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Typed canonical events. Shared rules and presentation consume the same positional receipts.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SourceObedienceEvent), "obedience")]
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
[JsonDerivedType(typeof(SourceMoveTargetResolvedEvent), "SourceMoveTargetResolvedEvent")]
[JsonDerivedType(typeof(SourceCalledOverlayChangedEvent), "SourceCalledOverlayChangedEvent")]
[JsonDerivedType(typeof(SourceCalledPartyChangedEvent), "SourceCalledPartyChangedEvent")]
[JsonDerivedType(typeof(SourceCalledPartySwitchRequiredEvent), "SourceCalledPartySwitchRequiredEvent")]
[JsonDerivedType(typeof(SourceCalledPartySwitchedEvent), "SourceCalledPartySwitchedEvent")]
[JsonDerivedType(typeof(SourceCalledSpikesChangedEvent), "SourceCalledSpikesChangedEvent")]
[JsonDerivedType(typeof(SourceCallerSelectionEvent), "SourceCallerSelectionEvent")]
[JsonDerivedType(typeof(SourceCaptureResultEvent), "SourceCaptureResultEvent")]
[JsonDerivedType(typeof(SourcePokedudeCaughtEvent), "SourcePokedudeCaughtEvent")]
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
[JsonDerivedType(typeof(SourceHeldItemHealEvent), "SourceHeldItemHealEvent")]
[JsonDerivedType(typeof(SourceHeldItemConsumedEvent), "SourceHeldItemConsumedEvent")]
[JsonDerivedType(typeof(SourceHeldItemKnockedOffEvent), "SourceHeldItemKnockedOffEvent")]
[JsonDerivedType(typeof(SourceDamageAdjustedEvent), "SourceDamageAdjustedEvent")]
[JsonDerivedType(typeof(SourceHeldItemSurvivalEvent), "SourceHeldItemSurvivalEvent")]
[JsonDerivedType(typeof(SourceHeldItemStolenEvent), "SourceHeldItemStolenEvent")]
[JsonDerivedType(typeof(SourceHeldItemRecycledEvent), "SourceHeldItemRecycledEvent")]
[JsonDerivedType(typeof(SourceHeldItemsSwappedEvent), "SourceHeldItemsSwappedEvent")]
[JsonDerivedType(typeof(SourceHpChangedEvent), "SourceHpChangedEvent")]
[JsonDerivedType(typeof(SourceInfatuationBlockedEvent), "SourceInfatuationBlockedEvent")]
[JsonDerivedType(typeof(SourceInfatuationChangedEvent), "SourceInfatuationChangedEvent")]
[JsonDerivedType(typeof(SourceIngrainHealEvent), "SourceIngrainHealEvent")]
[JsonDerivedType(typeof(SourceLeechSeedChangedEvent), "SourceLeechSeedChangedEvent")]
[JsonDerivedType(typeof(SourceLeechSeedDrainEvent), "SourceLeechSeedDrainEvent")]
[JsonDerivedType(typeof(SourceMagicCoatPressurePpEvent), "SourceMagicCoatPressurePpEvent")]
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
[JsonDerivedType(typeof(SourceRoamerRunEvent), "SourceRoamerRunEvent")]
[JsonDerivedType(typeof(Game.Domain.Trainers.SourceTrainerStartedEvent), "SourceTrainerStartedEvent")]
[JsonDerivedType(typeof(SourceTrainerOpponentSelectedEvent), "SourceTrainerOpponentSelectedEvent")]
[JsonDerivedType(typeof(SourceTrainerShiftOfferedEvent), "SourceTrainerShiftOfferedEvent")]
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
[JsonDerivedType(typeof(SourceWeatherContinuedEvent), "SourceWeatherContinuedEvent")]
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
public enum SourceMoveTargetResolutionReason { Retaliation }
/// <summary>A committed script target change after the invocation's earlier target selection.</summary>
public sealed record SourceMoveTargetResolvedEvent(long Sequence, int Turn, BattlerSlot Actor, MoveId CurrentMove,
    BattlerSlot PriorTarget, BattlerSlot Target, SourceMoveTargetResolutionReason Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCalledOverlayChangedEvent(long Sequence, int Turn, BattlerSlot Side, MoveId Move, WildBattleCalledOverlay? Before, WildBattleCalledOverlay? After) : SourceBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public BattlerSlot? Source { get; init; }
}
public sealed record SourceCalledPartyChangedEvent(long Sequence, int Turn, BattleSide Side, int PartyIndex, string Reason, WildBattleCalledPartyMember Before, WildBattleCalledPartyMember After) : SourceBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public BattlerSlot? SourceSlot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public byte? TransientMoveSlots { get; init; }
}
public sealed record SourceCalledPartySwitchRequiredEvent(long Sequence, int Turn, SourcePendingSwitch Pending) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCalledPartySwitchedEvent(long Sequence, int Turn, BattlerSlot Side, int OutgoingIndex, int IncomingIndex, WildBattleCalledSwitchKind Kind) : SourceBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceCombatantSnapshot? Replacement { get; init; }
}
public sealed record SourceCalledSpikesChangedEvent(long Sequence, int Turn, BattleSide Side, int Before, int After) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCallerSelectionEvent(long Sequence, int Turn, BattlerSlot Actor, MoveId Caller, MoveId? Result, ImmutableArray<int> CandidateSourceMoveIds, ulong RngCursor) : SourceBattleEvent(Sequence, Turn);
public sealed record SourcePokedudeCaughtEvent(long Sequence, int Turn, CaptureBall Ball, int AnimationCode, string Message) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCaptureResultEvent(long Sequence, int Turn, CaptureBall Ball, CaptureOutcome Outcome, CaptureDecisionBranch Branch, CaptureCalculation Calculation, int ChecksPassed, int AnimationCode) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceConfusionChangedEvent(long Sequence, int Turn, BattlerSlot Side, int Before, int After, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceConfusionSelfHitEvent(long Sequence, int Turn, BattlerSlot Side, MoveId SelectedMove, int HpBefore, int HpAfter, int Damage, int RandomPercent) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceCriticalHitEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceDefenseCurledEvent(long Sequence, int Turn, BattlerSlot Side) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceDisableChangedEvent(long Sequence, int Turn, BattlerSlot Side, MoveId? Move, int Before, int After, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceDrainEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, int DamageDealt, int HpBefore, int HpAfter, int Restored) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceEffectivenessEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, WildBattleEffectiveness Effectiveness) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceEncoreChangedEvent(long Sequence, int Turn, BattlerSlot Side, int? Slot, int Before, int After, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMoveEscapeReceipt(BattlerSlot Actor, MoveId Move);
public sealed record SourceEndedEvent(long Sequence, int Turn, WildBattleOutcome Outcome) : SourceBattleEvent(Sequence, Turn)
{
    // The successful move command owns its fled text. Ordinary Run, items and
    // forced expulsion retain their own receipts and never acquire this page.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceMoveEscapeReceipt? MoveEscape { get; init; }
}
public sealed record SourceFaintFriendshipReceipt(PartyMemberKey Member, int Before, int After);
public sealed record SourceFaintedEvent(long Sequence, int Turn, BattlerSlot Side) : SourceBattleEvent(Sequence, Turn)
{
    /// <summary>Permanent player-party friendship already applied by this faint command, including a clamped zero delta.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceFaintFriendshipReceipt? Friendship { get; init; }
}
public sealed record SourceFlinchEvent(long Sequence, int Turn, BattlerSlot Side, bool Applied) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceFocusEnergyEvent(long Sequence, int Turn, BattlerSlot Side) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceFocusPunchLostEvent(long Sequence, int Turn, BattlerSlot Actor, int PhysicalDamage, int SpecialDamage) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceFocusPunchPreparedEvent(long Sequence, int Turn, BattlerSlot Actor, int ChosenSlot, int PartyIndex) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceForcedOutEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, bool TrainerBattle) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceGrudgePpEvent(long Sequence, int Turn, BattlerSlot Fainted, BattlerSlot Attacker, MoveId Move, int ChosenSlot, int Before) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceHeldItemHealEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, string Item, int DamageReceipt, int HpBefore, int HpAfter, int HpRestored) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceHeldItemConsumedEvent(long Sequence, int Turn, BattlerSlot Side, string Item, int HpBefore, int HpAfter, int HpRestored) : SourceBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Game.Domain.Items.SourceItemPpRecovery? PpRecovery { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<SourceItemStatChange>? StatChanges { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? FocusEnergyRaised { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Flavor { get; init; }
}
/// <summary>Reached the source successful-hit resultmessage, after HP update.</summary>
public sealed record SourceHeldItemSurvivalEvent(long Sequence, int Turn, BattlerSlot Target, MoveId Move, string Item) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceDamageAdjustedEvent(long Sequence, int Turn, BattlerSlot Target, MoveId Move, string Item, int DamageBefore, int DamageAfter, bool Endured, bool HungOn) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceHeldItemKnockedOffEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, string Item) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceHeldItemStolenEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, string Item) : SourceBattleEvent(Sequence, Turn);
/// <summary>Successful recycle command, before animation and possible move-end reconsumption.</summary>
public sealed record SourceHeldItemRecycledEvent(long Sequence, int Turn, BattlerSlot Actor, MoveId Move, string Item) : SourceBattleEvent(Sequence, Turn);
/// <summary>Successful tryswapitems command; the actor receives TargetItemBefore during move-end.</summary>
public sealed record SourceHeldItemsSwappedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, string ActorItemBefore, string TargetItemBefore) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceHpChangedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, int HpBefore, int HpAfter, int Damage, bool Critical, int RandomPercent) : SourceBattleEvent(Sequence, Turn)
{
    /// <summary>Base power prepared by the executing dynamic-power command before damage calculation.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PreparedBasePower { get; init; }
    /// <summary>Source attack-animation power retained from CalculateBaseDamage after Sport and ability modifiers; zero is valid.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PreparedAnimationPower { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ushort? PreparedAnimationWeather { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PreparedFuryCutterCounter { get; init; }
}
public sealed record SourceInfatuationBlockedEvent(long Sequence, int Turn, BattlerSlot Side, BattlerSlot Source, MoveId Move) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceInfatuationChangedEvent(long Sequence, int Turn, BattlerSlot Side, BattlerSlot? Before, BattlerSlot? After, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceIngrainHealEvent(long Sequence, int Turn, BattlerSlot Side, int HpBefore, int HpAfter, int Restored) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceLeechSeedChangedEvent(long Sequence, int Turn, BattlerSlot Side, BattlerSlot? Before, BattlerSlot? After, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceLeechSeedDrainEvent(long Sequence, int Turn, BattlerSlot DrainedSide, BattlerSlot ReceivingSide, int HpBefore, int HpAfter, int ReceiverHpBefore, int ReceiverHpAfter, int Damage, int Restored) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMagicCoatPressurePpEvent(long Sequence, int Turn, BattlerSlot PressuringActor, BattlerSlot Reflector, int Slot, int Before, int After) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMagicCoatEvent(long Sequence, int Turn, BattlerSlot OriginalActor, BattlerSlot Reflector, MoveId Move) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMagnitudeEvent(long Sequence, int Turn, BattlerSlot Actor, int Magnitude, int Power) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceMedicineAppliedEvent(long Sequence, int Turn, ItemId Item, long TargetInstanceId, int PartyIndex, bool ActiveTarget, int HpBefore, int HpAfter, uint StatusBefore, uint StatusAfter) : SourceBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public BattlerSlot? Actor { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public BattleSide? TargetSide { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<Game.Domain.Items.SourceItemPpRecovery>? PpRecovery { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? FriendshipBefore { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? FriendshipAfter { get; init; }
}
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
public sealed record SourceTrainerOpponentSelectedEvent(long Sequence, int Turn, PartyMemberKey Incoming) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceTrainerShiftOfferedEvent(long Sequence, int Turn, PartyMemberKey Incoming) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceRoamerRunEvent(long Sequence, int Turn, bool Escaped) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceRunAttemptEvent(long Sequence, int Turn, int AttemptsBefore, int AttemptsAfter, int PlayerSpeed, int OpponentSpeed, int? Threshold, int? Roll, bool Escaped) : SourceBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? EscapingItem { get; init; }
}
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
public sealed record SourceSubstituteDamagedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, MoveId Move, int HpBefore, int HpAfter, int Damage, bool Critical, int RandomPercent) : SourceBattleEvent(Sequence, Turn)
{
    /// <summary>Base power prepared by the executing dynamic-power command before damage calculation.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PreparedBasePower { get; init; }
    /// <summary>Source attack-animation power retained from CalculateBaseDamage after Sport and ability modifiers; zero is valid.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PreparedAnimationPower { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ushort? PreparedAnimationWeather { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PreparedFuryCutterCounter { get; init; }
}
public sealed record SourceTowerBallDodgedEvent(long Sequence, int Turn, CaptureBall Ball) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceTowerGhostActionEvent(long Sequence, int Turn, BattlerSlot Side, string StringId) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceTowerHpEvent(long Sequence, int Turn, BattlerSlot Side, string Effect, int HpBefore, int HpAfter, int Damage) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceTowerVolatileEvent(long Sequence, int Turn, BattlerSlot Side, string Effect, bool Applied, string Reason) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceTransformedEvent(long Sequence, int Turn, BattlerSlot Actor, BattlerSlot Target, SpeciesId OriginalSpecies, SpeciesId AppearanceSpecies, uint AppearancePersonality, uint AppearanceOriginalTrainerId, PokemonInstance BattleView) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceTurnReadyEvent(long Sequence, int Turn, ushort RandomTurnNumber) : SourceBattleEvent(Sequence, Turn);
/// <summary>One continuing-weather presentation boundary, after any timer receipt
/// and before residual damage. Controller is frozen at field-phase entry.</summary>
public sealed record SourceWeatherContinuedEvent(long Sequence, int Turn, BattlerSlot Controller, string Weather) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceWeatherDamageEvent(long Sequence, int Turn, BattlerSlot Side, string Weather, int HpBefore, int HpAfter, int Damage) : SourceBattleEvent(Sequence, Turn);
public sealed record SourceYawnEvent(long Sequence, int Turn, BattlerSlot Side, int Before, int After, string Reason) : SourceBattleEvent(Sequence, Turn);

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
        if (value is SourceEndedEvent { MoveEscape: { } escaped } ended &&
            (ended.Outcome != WildBattleOutcome.Escaped || frame.Layout != SourceBattleLayout.Singles ||
             string.IsNullOrWhiteSpace(escaped.Move.Value))) return "source_event_move_escape";
        if (value is SourceWeatherContinuedEvent weather && weather.Weather is not ("rain" or "sandstorm" or "sun" or "hail"))
            return "source_event_weather_kind";
        if (value is SourceMoveTargetResolvedEvent resolved && (!Enum.IsDefined(resolved.Reason) ||
            resolved.PriorTarget == resolved.Target || resolved.Actor.Side == resolved.Target.Side))
            return "source_event_target_resolution";
        if (value is SourceOrderEvent order && (order.Order.IsDefault || order.Order.Length != frame.BattlerCount ||
            order.Order.Distinct().Count() != frame.BattlerCount)) return "source_event_order_shape";
        if (value is SourceFaintedEvent { Friendship: { } faintFriendship } faint &&
            (faint.Side.Side != BattleSide.Player || faint.Side.Value >= frame.BattlerCount ||
             faintFriendship.Member.Side != BattleSide.Player || frame.Get(faint.Side).Member != faintFriendship.Member ||
             frame.Get(faint.Side).Body?.Pokemon.CurrentHp != 0 || faintFriendship.Before is < 0 or > 255 ||
             faintFriendship.After < 0 || faintFriendship.After > faintFriendship.Before ||
             faintFriendship.Member.Index >= frame.Party(BattleSide.Player).Members.Length ||
             frame.Party(BattleSide.Player).Members[faintFriendship.Member.Index].Pokemon.Friendship != faintFriendship.After))
            return "source_event_faint_friendship";
        if (value is SourceCalledSpikesChangedEvent spikes && !Enum.IsDefined(spikes.Side) ||
            value is SourceCalledPartyChangedEvent party && (!Enum.IsDefined(party.Side) || party.PartyIndex < 0 || party.PartyIndex >= frame.Party(party.Side).Members.Length) ||
            value is SourceCalledBeatUpMemberEvent contributor && (!Enum.IsDefined(contributor.Side) || contributor.PartyIndex < 0 || contributor.PartyIndex >= frame.Party(contributor.Side).Members.Length))
            return "source_event_party_identity";
        if (value is SourceCalledPartyChangedEvent { SourceSlot: { } source } synchronized &&
            (synchronized.Reason is not ("active-sync" or "trick-party-write" or "recycle-party-write" or "changed-item-applied") || source.Value >= frame.BattlerCount ||
             frame.Get(source).Member != new PartyMemberKey(synchronized.Side, synchronized.PartyIndex)))
            return "source_event_active_sync_identity";
        if (value is SourceCalledPartyChangedEvent { Reason: "active-sync", SourceSlot: not null, TransientMoveSlots: null })
            return "source_event_active_sync_persistence";
        if (value is SourceCalledPartyChangedEvent { TransientMoveSlots: { } mask } persistence &&
            (persistence.Reason != "active-sync" || persistence.SourceSlot is not { } slot || slot.Value >= frame.BattlerCount ||
             frame.Get(slot).Body is not { } body || mask > 15 || mask != WildBattleTransformationRules.TransientMoveSlots(body)))
            return "source_event_active_sync_persistence";
        if (value is SourceMedicineAppliedEvent medicine && ((medicine.Actor is null) != (medicine.TargetSide is null) || medicine.Actor is { } actor &&
            (actor.Value >= frame.BattlerCount || frame.Get(actor).Body is null || medicine.TargetSide != actor.Side) ||
            medicine.TargetSide is { } targetSide && (!Enum.IsDefined(targetSide) || medicine.PartyIndex < 0 || medicine.PartyIndex >= frame.Party(targetSide).Members.Length) ||
            medicine.PpRecovery is { } pp && (pp.IsDefaultOrEmpty || pp.Any(p => p is null || p.Slot is < 0 or > 3 || p.Before < 0 || p.After <= p.Before) || pp.Select(p => p.Slot).Distinct().Count() != pp.Length) ||
            (medicine.FriendshipBefore is null) != (medicine.FriendshipAfter is null))) return "source_item_event_metadata";
        if (value is SourceMedicineAppliedEvent { TargetSide: { } itemSide } applied)
        {
            var member=frame.Party(itemSide).Members[applied.PartyIndex];
            if(member.Pokemon.CurrentHp!=applied.HpAfter || member.Status!=applied.StatusAfter ||
                applied.ActiveTarget!=frame.Slots.Any(c=>c.Member==new PartyMemberKey(itemSide,applied.PartyIndex)) ||
                applied.PpRecovery is { } recoveries && recoveries.Any(p=>p.Slot>=member.Pokemon.Moves.Length ||
                    member.Pokemon.Moves[p.Slot].Move!=p.Move || member.Pokemon.Moves[p.Slot].CurrentPp!=p.After) ||
                applied.FriendshipBefore is < 0 or > 255 || applied.FriendshipAfter is { } friendship && member.Pokemon.Friendship!=friendship)
                return "source_item_event_projection";
        }
        static bool PlayerKey(SourceBattleFrame f, PartyMemberKey key) => key.Side == BattleSide.Player && key.Index < f.Party(BattleSide.Player).Members.Length;
        static bool DefeatKey(SourceBattleFrame f, OccupantToken token) => token.Member.Side == BattleSide.Opponent &&
            token.Slot.Side == BattleSide.Opponent && f.IsCurrent(token) && f.Get(token.Slot).Body!.Pokemon.CurrentHp == 0;
        if (value is SourceRewardAppliedEvent reward && (!PlayerKey(frame, reward.Recipient) || !DefeatKey(frame, reward.Defeated)) ||
            value is SourceRewardLearningEvent learning && (!PlayerKey(frame, learning.Recipient) || !Enum.IsDefined(learning.Outcome)) ||
            value is SourceOpponentRewardCompletedEvent completed && !DefeatKey(frame, completed.Defeated))
            return "source_event_reward_identity";
        if (value is Game.Domain.Trainers.SourceTrainerStartedEvent started && (
            started.Trainer != (frame.TrainerEncounter?.Inventory.Trainer ?? frame.TrainerTowerEncounter?.Roster?.Trainer.Id) ||
            started.OpponentCount != frame.Party(BattleSide.Opponent).Members.Length))
            return "source_event_trainer_start_identity";
        var selectedTrainer = value switch { SourceTrainerOpponentSelectedEvent selected => selected.Incoming,
            SourceTrainerShiftOfferedEvent offered => offered.Incoming, _ => (PartyMemberKey?)null };
        if (selectedTrainer is { } incoming && (frame.TrainerEncounter is null || incoming.Side != BattleSide.Opponent ||
            frame.FourTurn?.TrainerReplacement?.Incoming != incoming)) return "source_event_trainer_replacement_identity";
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
            SourceObedienceEvent x => [x.Actor],
            SourceAttackStringEvent x => [x.Actor],
            SourceBallThrownEvent x => [x.Actor, x.Target],
            SourceBindingChangedEvent x => [x.Side],
            SourceBindingDamageEvent x => [x.Side],
            SourceCalledBeatUpMemberEvent x => [],
            SourceCalledDelayedEvent x => [x.Target],
            SourceCalledMoveTransitionEvent x => [x.Invocation.Actor, x.Invocation.Target, x.Invocation.PpOwner],
            SourceMoveTargetResolvedEvent x => [x.Actor, x.PriorTarget, x.Target],
            SourceCalledOverlayChangedEvent x => [x.Side, x.Source],
            SourceCalledPartyChangedEvent x => [x.SourceSlot],
            SourceCalledPartySwitchRequiredEvent x => [x.Pending.Slot, x.Pending.Invocation?.Actor, x.Pending.Invocation?.Target, x.Pending.Invocation?.PpOwner],
            SourceCalledPartySwitchedEvent x => [x.Side],
            SourceCalledSpikesChangedEvent x => [],
            SourceCallerSelectionEvent x => [x.Actor],
            SourceCaptureResultEvent x => [],
            SourcePokedudeCaughtEvent x => [],
            SourceConfusionChangedEvent x => [x.Side],
            SourceConfusionSelfHitEvent x => [x.Side],
            SourceCriticalHitEvent x => [x.Actor, x.Target],
            SourceDefenseCurledEvent x => [x.Side],
            SourceDisableChangedEvent x => [x.Side],
            SourceDrainEvent x => [x.Actor, x.Target],
            SourceEffectivenessEvent x => [x.Actor, x.Target],
            SourceEncoreChangedEvent x => [x.Side],
            SourceEndedEvent x => [x.MoveEscape?.Actor],
            SourceFaintedEvent x => [x.Side],
            SourceFlinchEvent x => [x.Side],
            SourceFocusEnergyEvent x => [x.Side],
            SourceFocusPunchLostEvent x => [x.Actor],
            SourceFocusPunchPreparedEvent x => [x.Actor],
            SourceForcedOutEvent x => [x.Actor, x.Target],
            SourceGrudgePpEvent x => [x.Fainted, x.Attacker],
            SourceHeldItemHealEvent x => [x.Actor, x.Target],
            SourceHeldItemConsumedEvent x => [x.Side],
            SourceDamageAdjustedEvent x => [x.Target],
            SourceHeldItemSurvivalEvent x => [x.Target],
            SourceHeldItemKnockedOffEvent x => [x.Actor, x.Target],
            SourceHeldItemStolenEvent x => [x.Actor, x.Target],
            SourceHeldItemRecycledEvent x => [x.Actor],
            SourceHeldItemsSwappedEvent x => [x.Actor, x.Target],
            SourceHpChangedEvent x => [x.Actor, x.Target],
            SourceInfatuationBlockedEvent x => [x.Side, x.Source],
            SourceInfatuationChangedEvent x => [x.Side, x.Before, x.After],
            SourceIngrainHealEvent x => [x.Side],
            SourceLeechSeedChangedEvent x => [x.Side, x.Before, x.After],
            SourceLeechSeedDrainEvent x => [x.DrainedSide, x.ReceivingSide],
            SourceMagicCoatPressurePpEvent x => [x.PressuringActor, x.Reflector],
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
            SourceRoamerRunEvent x => [],
            Game.Domain.Trainers.SourceTrainerStartedEvent x => [],
            SourceTrainerOpponentSelectedEvent x => [],
            SourceTrainerShiftOfferedEvent x => [],
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
            SourceWeatherContinuedEvent x => [x.Controller],
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
