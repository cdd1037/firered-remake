using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Capture;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.WildBattle;

public enum WildBattleSide { Player, Opponent }
public enum WildBattleOutcome { Ongoing = 0, Won = 1, Lost = 2, Draw = 3, Escaped = 4, Caught = 5 }
public enum WildBattleOrderPhase { Entry, Actions, EndTurn }
public enum WildBattleStat { Attack, Defense, Accuracy, Speed, Evasion, SpecialAttack, SpecialDefense }
public enum WildBattleEffectiveness { NoEffect, NotVeryEffective, SuperEffective }

public sealed record WildBattleCombatant(PokemonInstance Pokemon, int AttackStage = 6, int DefenseStage = 6,
    int AccuracyStage = 6, int EvasionStage = 6, int SpeedStage = 6,
    string HeldItem = "ITEM_NONE", uint Status = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool DefenseCurled = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int BindTurnsRemaining = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool AttackBadgeBoost = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WildBattleSide? LeechSeedSource = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int ConfusionTurnsRemaining = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool FocusEnergy = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Flinched = false)
{
    // Party-entry projection only. Active battle bodies must never carry an egg.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool IsEgg { get; init; }
    // gBattleStruct->choicedMove belongs to the live battler, never the owned party member.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MoveId? SourceChoiceMove { get; init; }
    // Opt-in called-action history. Historical battle profiles reject this extension;
    // use the versioned called-action contract to validate and resume its result.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSourceMoveHistory? SourceMoveHistory { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledDamageState? CalledDamage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledOverlay? CalledOverlay { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledTurnState? CalledTurnState { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledVolatileState? CalledVolatile { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int FuryCutterCounter { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleTransformation? Transform { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCopiedMoves? CopyMoves { get; init; }
    // Source Poké Flute may wake a party member independently of its current Soundproof battler.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool SourceTracePending { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public uint? SourcePartyStatusOverride { get; init; }
    // Historical profiles use this only for Sleep Talk. The explicit called-action
    // contract also carries an unlearned result, authenticated by SourceMoveHistory.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MoveId? LastCalledMove { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int YawnTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Cursed { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Nightmare { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool DestinyBond { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? MeanLookSource { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Rooted { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int FirstTurnCounter { get; init; }
    // Raw DisableStruct register; ability changes do not reset it. Switch/faint does.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool TruantLoafing { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool SpeedBadgeBoost { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool DefenseBadgeBoost { get; init; }
    // FireRed Badge07 boosts both special stats; omitted from all historical snapshots.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool SpecialBadgeBoost { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int SpecialAttackStageDelta { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int SpecialDefenseStageDelta { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ChargeTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int EncoreTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? EncoreMoveSlot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MoveId? EncoredMove { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool RampageUnableToMove { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int RampageTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? RampageMoveSlot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Rage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Identified { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool DamagedByOpponentThisTurn { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Protected { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Enduring { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int ProtectConsecutiveUses { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? KnockedOffItem { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Imprison { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool FlashFireBoost { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool WaterSport { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool MudSport { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Minimized { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MoveId? LastLandedMove { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? LastHitBy { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MoveId? LastUsedMove { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MoveId? DisabledMove { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int DisableTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int RolloutTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? RolloutMoveSlot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? InfatuationSource { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? DigMoveSlot { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MoveId? BindingMove { get; init; }
}
public sealed record WildBattleState(WildBattleCombatant Player, WildBattleCombatant Opponent,
    int Turn, long Revision, long EventSequence, RngState Rng, ushort RandomTurnNumber,
    WildBattleOutcome Outcome, int RunAttempts = 0, string RulesVersion = WildBattleRules.RulesVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool AwaitingReplacement = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ForestPlayerMoveSlot = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ForestOpponentMoveSlot = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ForestResidualsFinished = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int ForestPlayerLastMoveSlot = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int ForestOpponentLastMoveSlot = 0)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? SourceMoneyMultiplier { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceObedienceContext? Obedience { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceHitDamageReceipt? SourcePlayerHitReceipt { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceHitDamageReceipt? SourceOpponentHitReceipt { get; init; }
    // SpecialStatus.focusBanded is owned by numeric battler slots and survives residual callbacks.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public byte SourceFocusBandedMask { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourcePermanentWeather? PermanentWeather { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SourceMechanicsRulesVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SourceCampaignRulesVersion { get; init; }
    [JsonIgnore] public GamePokemonCatalog? Campaign { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSourceWildState? SourceWild { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledFieldState? CalledField { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledPartyState? CalledParty { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleCalledDelayedState? CalledDelayed { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool ScriptedSnorlax { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public PokemonTowerBattleKind? TowerBattleKind { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleTerrain? Terrain { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PlayerReflectTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int OpponentReflectTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int SandstormTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int RainTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PayDayMoney { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(WildBattleObedienceEvent), "obedience")]
[JsonDerivedType(typeof(WildBattlePermanentWeatherChangedEvent), "permanent-weather-changed")]
[JsonDerivedType(typeof(WildBattleAbilityRecoveredEvent), "ability-recovered")]
[JsonDerivedType(typeof(WildBattleCalledOverlayChangedEvent), "called-party-overlaychanged")]
[JsonDerivedType(typeof(WildBattleCalledPartyChangedEvent), "called-party-partychanged")]
[JsonDerivedType(typeof(WildBattleCalledPartySwitchRequiredEvent), "called-party-partyswitchrequired")]
[JsonDerivedType(typeof(WildBattleCalledPartySwitchedEvent), "called-party-partyswitched")]
[JsonDerivedType(typeof(WildBattleCalledSpikesChangedEvent), "called-party-spikeschanged")]
[JsonDerivedType(typeof(WildBattleCalledBeatUpMemberEvent), "called-party-beatupmember")]
[JsonDerivedType(typeof(WildBattleSubstituteDamagedEvent), "substitute-damaged")]
[JsonDerivedType(typeof(WildBattleCalledDelayedEvent), "called-delayed")]
[JsonDerivedType(typeof(WildBattleMagicCoatPressurePpEvent), "magic-coat-pressure-pp")]
[JsonDerivedType(typeof(WildBattleMagicCoatEvent), "magic-coat")]
[JsonDerivedType(typeof(WildBattleFocusPunchPreparedEvent), "focus-punch-prepared")]
[JsonDerivedType(typeof(WildBattleFocusPunchLostEvent), "focus-punch-lost")]
[JsonDerivedType(typeof(WildBattleSnatchReadyEvent), "snatch-ready")]
[JsonDerivedType(typeof(WildBattleMoveSnatchedEvent), "move-snatched")]
[JsonDerivedType(typeof(WildBattleSnatchPressurePpEvent), "snatch-pressure-pp")]
[JsonDerivedType(typeof(WildBattleGrudgePpEvent), "grudge-pp")]
[JsonDerivedType(typeof(WildBattleCallerSelectionEvent), "caller-selection")]
[JsonDerivedType(typeof(WildBattleCalledMoveTransitionEvent), "called-move-transition")]
[JsonDerivedType(typeof(WildBattleMoveTargetResolvedEvent), "move-target-resolved")]
[JsonDerivedType(typeof(WildBattleAttackStringEvent), "attack-string")]
[JsonDerivedType(typeof(WildBattlePpCommandEvent), "pp-command")]
[JsonDerivedType(typeof(WildBattleTransformedEvent), "transformed")]
[JsonDerivedType(typeof(WildBattleMoveCopiedEvent), "move-copied")]
[JsonDerivedType(typeof(WildBattleYawnEvent), "yawn")]
[JsonDerivedType(typeof(WildBattleSleepTalkEvent), "sleep-talk")]
[JsonDerivedType(typeof(WildBattlePokeFluteEvent), "poke-flute")]
[JsonDerivedType(typeof(WildBattleTowerGhostActionEvent), "tower-ghost-action")]
[JsonDerivedType(typeof(WildBattleTowerBallDodgedEvent), "tower-ball-dodged")]
[JsonDerivedType(typeof(WildBattleTowerVolatileEvent), "tower-volatile")]
[JsonDerivedType(typeof(WildBattleSpiteEvent), "spite")]
[JsonDerivedType(typeof(WildBattleTowerHpEvent), "tower-hp")]
[JsonDerivedType(typeof(WildBattleHeldItemKnockedOffEvent), "held-item-knocked-off")]
[JsonDerivedType(typeof(WildBattleDamageAdjustedEvent), "source-damage-adjusted")]
[JsonDerivedType(typeof(WildBattleHeldItemSurvivalEvent), "held-item-survival")]
[JsonDerivedType(typeof(WildBattleWeatherContinuedEvent), "weather-continued")]
[JsonDerivedType(typeof(WildBattleWeatherDamageEvent), "weather-damage")]
[JsonDerivedType(typeof(WildBattleProtectAttemptEvent), "protect-attempt")]
[JsonDerivedType(typeof(WildBattlePayDayEvent), "pay-day")]
[JsonDerivedType(typeof(WildBattleStartedEvent), "started")]
[JsonDerivedType(typeof(WildBattleOrderEvent), "order")]
[JsonDerivedType(typeof(WildBattleMoveUsedEvent), "move-used")]
[JsonDerivedType(typeof(WildBattleMoveMissedEvent), "move-missed")]
[JsonDerivedType(typeof(WildBattleHpChangedEvent), "hp-changed")]
[JsonDerivedType(typeof(WildBattlePotionRecoveredEvent), "potion-recovered")]
[JsonDerivedType(typeof(WildBattleMedicineAppliedEvent), "medicine-applied")]
[JsonDerivedType(typeof(WildBattleRecoilEvent), "recoil")]
[JsonDerivedType(typeof(WildBattleCriticalHitEvent), "critical-hit")]
[JsonDerivedType(typeof(WildBattleStatChangedEvent), "stat-changed")]
[JsonDerivedType(typeof(WildBattleStatAtMinimumEvent), "stat-at-minimum")]
[JsonDerivedType(typeof(WildBattleAbilityPreventedEvent), "ability-prevented")]
[JsonDerivedType(typeof(WildBattleFaintedEvent), "fainted")]
[JsonDerivedType(typeof(WildBattleEndedEvent), "ended")]
[JsonDerivedType(typeof(WildBattleTurnReadyEvent), "turn-ready")]
[JsonDerivedType(typeof(WildBattleRunAttemptEvent), "run-attempt")]
[JsonDerivedType(typeof(WildBattleBallThrownEvent), "ball-thrown")]
[JsonDerivedType(typeof(WildBattleCaptureResultEvent), "capture-result")]
[JsonDerivedType(typeof(WildBattleReplacementRequiredEvent), "replacement-required")]
[JsonDerivedType(typeof(WildBattlePlayerReplacedEvent), "player-replaced")]
[JsonDerivedType(typeof(WildBattlePlayerSwitchedEvent), "player-switched")]
[JsonDerivedType(typeof(WildBattleStatusChangedEvent), "status-changed")]
[JsonDerivedType(typeof(WildBattleStatusDamageEvent), "status-damage")]
[JsonDerivedType(typeof(WildBattleActionBlockedEvent), "action-blocked")]
[JsonDerivedType(typeof(WildBattleStatAtMaximumEvent), "stat-at-maximum")]
[JsonDerivedType(typeof(WildBattleBindingChangedEvent), "binding-changed")]
[JsonDerivedType(typeof(WildBattleBindingDamageEvent), "binding-damage")]
[JsonDerivedType(typeof(WildBattleDefenseCurledEvent), "defense-curled")]
[JsonDerivedType(typeof(WildBattleEffectivenessEvent), "effectiveness")]
[JsonDerivedType(typeof(WildBattleMoveFailedEvent), "move-failed")]
[JsonDerivedType(typeof(WildBattleLeechSeedChangedEvent), "leech-seed-changed")]
[JsonDerivedType(typeof(WildBattleLeechSeedDrainEvent), "leech-seed-drain")]
[JsonDerivedType(typeof(WildBattleConfusionChangedEvent), "confusion-changed")]
[JsonDerivedType(typeof(WildBattleConfusionSelfHitEvent), "confusion-self-hit")]
[JsonDerivedType(typeof(WildBattleFocusEnergyEvent), "focus-energy")]
[JsonDerivedType(typeof(WildBattleFlinchEvent), "flinch")]
[JsonDerivedType(typeof(WildBattleMultiHitEvent), "multi-hit")]
[JsonDerivedType(typeof(WildBattleForcedOutEvent), "forced-out")]
[JsonDerivedType(typeof(WildBattleRoamerRunEvent), "roamer-run")]
[JsonDerivedType(typeof(WildBattlePursuitInterceptedEvent), "pursuit-intercepted")]
[JsonDerivedType(typeof(WildBattleDisableChangedEvent), "disable-changed")]
[JsonDerivedType(typeof(WildBattleRolloutChangedEvent), "rollout-changed")]
[JsonDerivedType(typeof(WildBattleInfatuationChangedEvent), "infatuation-changed")]
[JsonDerivedType(typeof(WildBattleInfatuationBlockedEvent), "infatuation-blocked")]
[JsonDerivedType(typeof(WildBattleAbilityActivatedEvent), "ability-activated")]
[JsonDerivedType(typeof(WildBattleAbilityDamageEvent), "ability-damage")]
[JsonDerivedType(typeof(WildBattleHeldItemHealEvent), "held-item-heal")]
[JsonDerivedType(typeof(WildBattleHeldItemConsumedEvent), "held-item-consumed")]
[JsonDerivedType(typeof(WildBattleMtMoonVolatileEvent), "mtmoon-volatile")]
[JsonDerivedType(typeof(WildBattleEncoreChangedEvent), "encore-changed")]
[JsonDerivedType(typeof(WildBattleDrainEvent), "drain")]
[JsonDerivedType(typeof(WildBattleRootsChangedEvent), "roots-changed")]
[JsonDerivedType(typeof(WildBattleIngrainHealEvent), "ingrain-heal")]
[JsonDerivedType(typeof(WildBattleRecoveredEvent), "recovered")]
[JsonDerivedType(typeof(WildBattleMagnitudeEvent), "magnitude")]
[JsonDerivedType(typeof(WildBattleSelfDestructEvent), "self-destruct")]
[JsonDerivedType(typeof(WildBattleHeldItemStolenEvent), "held-item-stolen")]
[JsonDerivedType(typeof(WildBattleHeldItemRecycledEvent), "held-item-recycled")]
[JsonDerivedType(typeof(WildBattleHeldItemsSwappedEvent), "held-items-swapped")]
public abstract record WildBattleEvent(long Sequence, int Turn);
public sealed record WildBattleStartedEvent(long Sequence, int Turn) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleOrderEvent(long Sequence, int Turn, WildBattleOrderPhase Phase,
    WildBattleSide First, WildBattleSide Second) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleMoveUsedEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, int Slot, MoveId Move, int PpBefore, int PpAfter) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleMoveMissedEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, MoveId Move) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleHpChangedEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, MoveId Move, int HpBefore, int HpAfter, int Damage, bool Critical,
    int RandomPercent) : WildBattleEvent(Sequence, Turn)
{
    /// <summary>Base power prepared by the executing dynamic-power command before damage calculation.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PreparedBasePower { get; init; }
    /// <summary>Source attack-animation power retained from CalculateBaseDamage after Sport and ability modifiers; zero is valid.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PreparedAnimationPower { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ushort? PreparedAnimationWeather { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PreparedFuryCutterCounter { get; init; }
}
public sealed record WildBattlePotionRecoveredEvent(long Sequence, int Turn, WildBattleSide Side,
    int HpBefore, int HpAfter, int HpRestored) : WildBattleEvent(Sequence, Turn);
/// <summary>Exact owned target and medicine-only delta, before the enemy response and residuals.</summary>
public sealed record WildBattleMedicineAppliedEvent(long Sequence, int Turn, ItemId Item,
    long TargetInstanceId, int PartyIndex, bool ActiveTarget, int HpBefore, int HpAfter,
    uint StatusBefore, uint StatusAfter) : WildBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? Actor { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? TargetSide { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<Game.Domain.Items.SourceItemPpRecovery>? PpRecovery { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? FriendshipBefore { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? FriendshipAfter { get; init; }
}
public sealed record WildBattleRecoilEvent(long Sequence, int Turn, WildBattleSide Actor,
    MoveId Move, int HpBefore, int HpAfter, int Damage) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleCriticalHitEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleStatChangedEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, WildBattleStat Stat, int Before, int After) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleStatAtMinimumEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, WildBattleStat Stat) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleAbilityPreventedEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, MoveId Move, string Ability) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleFaintedEvent(long Sequence, int Turn, WildBattleSide Side) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleMoveEscapeReceipt(WildBattleSide Actor, MoveId Move);
public sealed record WildBattleEndedEvent(long Sequence, int Turn, WildBattleOutcome Outcome) : WildBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleMoveEscapeReceipt? MoveEscape { get; init; }
}
public sealed record WildBattleTurnReadyEvent(long Sequence, int Turn, ushort RandomTurnNumber) : WildBattleEvent(Sequence, Turn);
/// <summary>Threshold is the source u8 value, null for guaranteed escape. Roll is null if no escape draw occurs.</summary>
public sealed record WildBattleRunAttemptEvent(long Sequence, int Turn, int AttemptsBefore, int AttemptsAfter,
    int PlayerSpeed, int OpponentSpeed, int? Threshold, int? Roll, bool Escaped) : WildBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? EscapingItem { get; init; }
}
public sealed record WildBattleBallThrownEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, CaptureBall Ball) : WildBattleEvent(Sequence, Turn);
/// <summary>AnimationCode 4 means three-shake success, not four visible shakes. The captured instance
/// is WildBattleTransformationRules.PersistentPokemon(State.Opponent); ownership, inventory and Pokédex effects belong to the caller.</summary>
public sealed record WildBattleCaptureResultEvent(long Sequence, int Turn, CaptureBall Ball,
    CaptureOutcome Outcome, CaptureDecisionBranch Branch, CaptureCalculation Calculation,
    int ChecksPassed, int AnimationCode) : WildBattleEvent(Sequence, Turn);
/// <summary>The active player fainted but the caller reports a living reserve. The current turn is paused.</summary>
public sealed record WildBattleReplacementRequiredEvent(long Sequence, int Turn) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattlePlayerReplacedEvent(long Sequence, int Turn,
    WildBattleCombatant Previous, WildBattleCombatant Replacement) : WildBattleEvent(Sequence, Turn);
/// <summary>A voluntary switch. Previous includes any Route22 Pursuit interception damage; an intercepted
/// action has already executed, otherwise the opponent's selected action still follows the switch.</summary>
public sealed record WildBattlePlayerSwitchedEvent(long Sequence, int Turn,
    WildBattleCombatant Previous, WildBattleCombatant Replacement) : WildBattleEvent(Sequence, Turn);

/// <summary>Failure returns the identical input state, empty events and trace. Start failure returns null.</summary>
public sealed record WildBattleResult(TransitionStatus Status, WildBattleState? State,
    ImmutableArray<WildBattleEvent> Events, ImmutableArray<RandomTrace> Trace, Diagnostic? Diagnostic)
{
    public bool Succeeded => Status == TransitionStatus.Accepted;
    public string? Fault => Diagnostic?.Code;
}

/// <summary>Reason is move, static, shed-skin or faint; uint values are exact Gen III status1.</summary>
public sealed record WildBattleStatusChangedEvent(long Sequence, int Turn, WildBattleSide Side,
    uint Before, uint After, string Reason) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleStatusDamageEvent(long Sequence, int Turn, WildBattleSide Side,
    uint Status, int HpBefore, int HpAfter, int Damage) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleActionBlockedEvent(long Sequence, int Turn, WildBattleSide Side,
    MoveId Move, uint Status) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleStatAtMaximumEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, WildBattleStat Stat) : WildBattleEvent(Sequence, Turn);

/// <summary>Remaining is the source status2 counter: 3-6 on application, decremented before residual damage.</summary>
public sealed record WildBattleBindingChangedEvent(long Sequence, int Turn, WildBattleSide Side,
    int Before, int After, string Reason) : WildBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MoveId? Move { get; init; }
}
public sealed record WildBattleBindingDamageEvent(long Sequence, int Turn, WildBattleSide Side,
    MoveId Move, int HpBefore, int HpAfter, int Damage, int Remaining) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleDefenseCurledEvent(long Sequence, int Turn, WildBattleSide Side) : WildBattleEvent(Sequence, Turn);

/// <summary>Non-neutral source result message, after HP/critical messages and before secondary effects.</summary>
public sealed record WildBattleEffectivenessEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, MoveId Move, WildBattleEffectiveness Effectiveness) : WildBattleEvent(Sequence, Turn);

/// <summary>Source failure branch after spending PP, with no status/seed application.</summary>
public sealed record WildBattleMoveFailedEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, MoveId Move, string Reason) : WildBattleEvent(Sequence, Turn);
/// <summary>Source is a battler slot, not a Pokemon identity. It survives the caster leaving.</summary>
public sealed record WildBattleLeechSeedChangedEvent(long Sequence, int Turn, WildBattleSide Side,
    WildBattleSide? Before, WildBattleSide? After, string Reason) : WildBattleEvent(Sequence, Turn);
/// <summary>Damage and healing are actual clamped HP deltas. Drain and heal precede the donor's faint message.</summary>
public sealed record WildBattleLeechSeedDrainEvent(long Sequence, int Turn, WildBattleSide DrainedSide,
    WildBattleSide ReceivingSide, int HpBefore, int HpAfter, int ReceiverHpBefore, int ReceiverHpAfter,
    int Damage, int Restored) : WildBattleEvent(Sequence, Turn);

/// <summary>The Gen III status2 confusion counter: 2-5 at application, decremented before the action check.</summary>
public sealed record WildBattleConfusionChangedEvent(long Sequence, int Turn, WildBattleSide Side,
    int Before, int After, string Reason) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleConfusionSelfHitEvent(long Sequence, int Turn, WildBattleSide Side,
    MoveId SelectedMove, int HpBefore, int HpAfter, int Damage, int RandomPercent) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleFocusEnergyEvent(long Sequence, int Turn, WildBattleSide Side) : WildBattleEvent(Sequence, Turn);
/// <summary>Applied is true when flinch is set before the target's action, false when that action is blocked.</summary>
public sealed record WildBattleFlinchEvent(long Sequence, int Turn, WildBattleSide Side, bool Applied) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleMultiHitEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, MoveId Move, int PlannedHits, int ActualHits) : WildBattleEvent(Sequence, Turn);
/// <summary>Wild force-out ends the encounter without rewards; trainer force-out is followed by a typed opponent switch.</summary>
public sealed record WildBattleForcedOutEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, MoveId Move, bool TrainerBattle) : WildBattleEvent(Sequence, Turn);

/// <summary>The chosen Pursuit intercepts a voluntary switch and consumes its original queued action.</summary>
public sealed record WildBattlePursuitInterceptedEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide SwitchingSide, int Slot) : WildBattleEvent(Sequence, Turn);

/// <summary>Disable lasts 2–5 source end-turn ticks including the application turn.</summary>
public sealed record WildBattleDisableChangedEvent(long Sequence, int Turn, WildBattleSide Side,
    MoveId? Move, int Before, int After, string Reason) : WildBattleEvent(Sequence, Turn);
/// <summary>Remaining is the number of forced Rollout attacks left after this successful hit.</summary>
public sealed record WildBattleRolloutChangedEvent(long Sequence, int Turn, WildBattleSide Side,
    int Before, int After, string Reason) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleInfatuationChangedEvent(long Sequence, int Turn, WildBattleSide Side,
    WildBattleSide? Before, WildBattleSide? After, string Reason) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleInfatuationBlockedEvent(long Sequence, int Turn, WildBattleSide Side,
    WildBattleSide Source, MoveId Move) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleAbilityActivatedEvent(long Sequence, int Turn, WildBattleSide Side,
    string Ability) : WildBattleEvent(Sequence, Turn);
/// <summary>Passive ability damage, separate from move damage and recoil. Does not feed Counter,
/// Bide, Rage, Destiny Bond, or contact triggers. Damage is the clamped HP loss.</summary>
public sealed record WildBattleAbilityDamageEvent(long Sequence, int Turn, WildBattleSide Holder,
    WildBattleSide Target, MoveId TriggeringMove, string Ability, int HpBefore, int HpAfter,
    int Damage) : WildBattleEvent(Sequence, Turn);
/// <summary>Source Oran Berry end-turn healing and one-time consumption; no RNG is consumed.</summary>
public sealed record WildBattleHeldItemHealEvent(long Sequence, int Turn, WildBattleSide Actor, WildBattleSide Target, MoveId Move, string Item, int DamageReceipt, int HpBefore, int HpAfter, int HpRestored) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleHeldItemConsumedEvent(long Sequence, int Turn, WildBattleSide Side,
    string Item, int HpBefore, int HpAfter, int HpRestored) : WildBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Game.Domain.Items.SourceItemPpRecovery? PpRecovery { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<Game.Domain.Battle.SourceItemStatChange>? StatChanges { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? FocusEnergyRaised { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Flavor { get; init; }
}

public sealed record WildBattleMtMoonVolatileEvent(long Sequence, int Turn, WildBattleSide Side,
    string Volatile, int Before, int After, string Reason) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleEncoreChangedEvent(long Sequence, int Turn, WildBattleSide Side,
    int? Slot, int Before, int After, string Reason) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleDrainEvent(long Sequence, int Turn, WildBattleSide Actor, WildBattleSide Target,
    MoveId Move, int DamageDealt, int HpBefore, int HpAfter, int Restored) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleMagnitudeEvent(long Sequence, int Turn, WildBattleSide Actor,
    int Magnitude, int Power) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleSelfDestructEvent(long Sequence, int Turn, WildBattleSide Actor,
    MoveId Move, int HpBefore) : WildBattleEvent(Sequence, Turn);

public sealed record WildBattleHeldItemStolenEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, MoveId Move, string Item) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleHeldItemRecycledEvent(long Sequence, int Turn, WildBattleSide Actor,
    MoveId Move, string Item) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleHeldItemsSwappedEvent(long Sequence, int Turn, WildBattleSide Actor,
    WildBattleSide Target, MoveId Move, string ActorItemBefore, string TargetItemBefore) : WildBattleEvent(Sequence, Turn);

public sealed record WildBattleRecoveredEvent(long Sequence, int Turn, WildBattleSide Side, MoveId Move, int HpBefore, int HpAfter, int Recovered) : WildBattleEvent(Sequence, Turn);

public sealed record WildBattlePayDayEvent(long Sequence, int Turn, WildBattleSide Actor, int Before, int After) : WildBattleEvent(Sequence, Turn);

public sealed record WildBattleHeldItemKnockedOffEvent(long Sequence, int Turn, WildBattleSide Actor, WildBattleSide Target, MoveId Move, string Item) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleWeatherContinuedEvent(long Sequence, int Turn, WildBattleSide Controller, string Weather) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleWeatherDamageEvent(long Sequence, int Turn, WildBattleSide Side, string Weather, int HpBefore, int HpAfter, int Damage) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleProtectAttemptEvent(long Sequence, int Turn, WildBattleSide Side, MoveId Move, int Before, int After, int Threshold, int Roll, bool NotLastAction, bool Succeeded) : WildBattleEvent(Sequence, Turn);

public sealed record WildBattleRootsChangedEvent(long Sequence, int Turn, WildBattleSide Side, bool Rooted, string Reason) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleIngrainHealEvent(long Sequence, int Turn, WildBattleSide Side, int HpBefore, int HpAfter, int Restored) : WildBattleEvent(Sequence, Turn);

/// <summary>Source canceller text; no move-use or PP expenditure occurs.</summary>
public sealed record WildBattleTowerGhostActionEvent(long Sequence, int Turn, WildBattleSide Side, string StringId) : WildBattleEvent(Sequence, Turn);
/// <summary>Source BALL_GHOST_DODGE, then the opponent action. Caller spends the authorized ball.</summary>
public sealed record WildBattleTowerBallDodgedEvent(long Sequence, int Turn, CaptureBall Ball) : WildBattleEvent(Sequence, Turn);

public sealed record WildBattlePermanentWeatherChangedEvent(long Sequence, int Turn, WildBattleSide Side, string Ability,
    SourcePermanentWeatherKind? Before, SourcePermanentWeatherKind After) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleAbilityRecoveredEvent(long Sequence, int Turn, WildBattleSide Side, string Ability,
    int HpBefore, int HpAfter, int Recovered) : WildBattleEvent(Sequence, Turn);

/// <summary>The source adjustment command result; display and HP application may follow later.</summary>
public sealed record WildBattleDamageAdjustedEvent(long Sequence, int Turn, WildBattleSide Target, MoveId Move, string Item,
    int DamageBefore, int DamageAfter, bool Endured, bool HungOn) : WildBattleEvent(Sequence, Turn);

/// <summary>Successful source item-survival result message; not item consumption.</summary>
public sealed record WildBattleHeldItemSurvivalEvent(long Sequence, int Turn, WildBattleSide Target, MoveId Move, string Item) : WildBattleEvent(Sequence, Turn);
