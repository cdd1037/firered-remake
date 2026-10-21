using System.Text.Json.Serialization;
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
