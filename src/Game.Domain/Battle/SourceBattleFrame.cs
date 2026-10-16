using Game.Domain.Pokemon;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public sealed record SourceBattleParty(BattleSide Side, ImmutableArray<WildBattleCalledPartyMember> Members);

public sealed record SourceMoveHistory
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    public MoveId? LastPrintedMove { get; init; }
    public MoveId? LastResultingMove { get; init; }
    public string? LastHitByType { get; init; }
    public MoveId? LastTakenMove { get; init; }
    public ImmutableArray<MoveId?> LastTakenFrom { get; init; } = [null, null, null, null];
}
public sealed record SourceDamageRegisters(string RulesVersion, int Turn, int PhysicalDamage,
    BattlerSlot? PhysicalSource, int SpecialDamage, BattlerSlot? SpecialSource);
public sealed record SourceTurnLock(WildBattleCalledLockKind Kind, MoveId Move, int PpSlot, MoveId PpSlotMove,
    BattlerSlot Target, int TurnsRemaining, int StoredDamage, BattlerSlot? StoredDamageSource, bool UnableToUseMove);
public sealed record SourceTurnRegisters(string RulesVersion, SourceTurnLock? Lock,
    int SureHitTurnsRemaining, BattlerSlot? SureHitSource);

/// <summary>The only authority for live battler references; the reusable scalar body must contain none.</summary>
public sealed record SourceSlotRegisters
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool HelpingHand { get; init; }
    public BattlerSlot? LeechSeedSource { get; init; }
    public BattlerSlot? MeanLookSource { get; init; }
    public BattlerSlot? LastHitBy { get; init; }
    public BattlerSlot? InfatuationSource { get; init; }
    public BattlerSlot? WrappedBy { get; init; }
    public SourceMoveHistory? History { get; init; }
    public SourceDamageRegisters? Damage { get; init; }
    public SourceTurnRegisters? Turn { get; init; }
    public int? ChosenMoveSlot { get; init; }
    public int LastMoveSlot { get; init; }
}
public sealed record SourceFutureAttack(int TurnsRemaining, MoveId Move, BattlerSlot Attacker, int Damage);
/// <summary>These records survive an occupant change independently of combatant volatiles.</summary>
public sealed record SourcePositionRegisters
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceHitDamageReceipt? HitItemReceipt { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool FocusBanded { get; init; }
    public WildBattleCalledWish? Wish { get; init; }
    public SourceFutureAttack? FutureAttack { get; init; }
    public string? UsedHeldItem { get; init; }
}
public sealed record SourceBattleSlot(BattlerSlot Slot, PartyMemberKey? Member, long EntrySerial,
    WildBattleCombatant? Body)
{
    public SourceSlotRegisters Registers { get; init; } = new();
    public SourcePositionRegisters Position { get; init; } = new();
    [JsonIgnore] public OccupantToken? Occupant => Member is { } key && Body is not null
        ? new(Slot, key, EntrySerial) : null;
}
public sealed record SourceSideRegisters(BattleSide Side)
{
    public int ReflectTurnsRemaining { get; init; }
    public int LightScreenTurnsRemaining { get; init; }
    public int MistTurnsRemaining { get; init; }
    public int SafeguardTurnsRemaining { get; init; }
    public int Spikes { get; init; }
    public int FollowMeTimer { get; init; }
    public BattlerSlot? FollowMeTarget { get; init; }
}
public sealed record SourceFieldRegisters
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? MoneyMultiplier { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourcePermanentWeather? PermanentWeather { get; init; }
    public int SandstormTurnsRemaining { get; init; }
    public int RainTurnsRemaining { get; init; }
    public int SunTurnsRemaining { get; init; }
    public int HailTurnsRemaining { get; init; }
    public int PayDayMoney { get; init; }
    public WildBattleTerrain? Terrain { get; init; }
}
public enum SourceActionKind { Move, Item, Switch, Cancelled, Run, Capture }
public sealed record SourceActionSelection(OccupantToken Actor, SourceActionKind Kind,
    int? MoveSlot = null, BattlerSlot? Target = null, PartyMemberKey? Incoming = null, ItemId? Item = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceCaptureSelection? Capture { get; init; }
    [JsonIgnore] public bool IsStruggle => Kind == SourceActionKind.Move && MoveSlot == WildBattleRules.StruggleSlot;
    public static SourceActionSelection Struggle(OccupantToken actor, BattlerSlot? target = null) =>
        new(actor, SourceActionKind.Move, WildBattleRules.StruggleSlot, target ?? actor.Slot.Opposite);
    // The synchronous trainer chooser already consumed AreAllMovesUnusable's selection draw.
    // This ephemeral receipt is not battle state; action-time target selection still rerolls.
    [JsonIgnore] internal bool StruggleSelectionPrepared { get; init; }
}
public sealed record SourceQueuedAction(BattlerSlot Slot, SourceActionToken? Token, SourceActionKind Kind,
    int? MoveSlot = null, BattlerSlot? Target = null, PartyMemberKey? Incoming = null, ItemId? Item = null,
    string? CancellationReason = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceCaptureSelection? Capture { get; init; }
    // gChosenMoveByBattler survives learning that overwrites the selected move position.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MoveId? OriginalChosenMove { get; init; }
}
public sealed record SourceSwitchReservation(OccupantToken Outgoing, PartyMemberKey Incoming,
    WildBattleCalledSwitchKind Kind);
/// <summary>Player medicine is paid/applied in the party selection menu, before the turn queue.
/// This bounded, current-turn receipt prevents the later item action from applying it again.</summary>
public sealed record SourcePreparedMedicine(OccupantToken Actor, ItemId Item, PartyMemberKey Target)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool EscapeOnUse { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Game.Domain.Items.SourceBattleItemDefinition? DeferredEffect { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte SourceAbsentMaskBeforeSelection { get; init; }
}
public sealed record SourceMoveInvocation(BattlerSlot Actor, int ChosenSlot, int PpSlot,
    MoveId OriginalChosenMove, MoveId ChosenMove, MoveId CurrentMove, MoveId? CalledMove,
    BattlerSlot Target, WildBattleAttackCancellerPhase CancellerPhase, WildBattleInvocationFlags Flags)
{
    public BattlerSlot PpOwner { get; init; } = Actor;
}
public sealed record SourcePendingSwitch(BattlerSlot Slot, WildBattleCalledSwitchKind Kind,
    ImmutableArray<int> LegalPartyIndices, SourceMoveInvocation? Invocation);
public sealed record SourceEndTurnCursor(int Turn, ImmutableArray<BattlerSlot> Order,
    WildBattleSourceEndTurnStage Stage, int NextBattler, long MinimumRevision, long MinimumEventSequence,
    string RulesVersion);
/// <summary>Canonical identity/state substrate. This type alone admits no executable doubles recipes.</summary>
public sealed record SourceBattleFrame
{
    public const string IdentityVersion = "firered-source-battler-identity-v1";
    public string Version { get; init; } = IdentityVersion;
    public string SourceCommit { get; init; } = WildBattleRules.SourceCommit;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceObedienceContext? Obedience { get; init; }
    public string RulesVersion { get; init; } = WildBattleRules.FuchsiaRulesVersion;
    public string? SourceMechanicsRulesVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SourceCampaignRulesVersion { get; init; }
    [JsonIgnore] public GamePokemonCatalog? Campaign { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourcePokedudeContext? Pokedude { get; init; }
    public SourceBattleLayout Layout { get; init; }
    public KoReplacementTiming KoReplacementTiming { get; init; }
    public ImmutableArray<SourceBattleParty> Parties { get; init; } = [];
    public ImmutableArray<SourceBattleSlot> Slots { get; init; } = [];
    public ImmutableArray<SourceSideRegisters> Sides { get; init; } = [new(BattleSide.Player), new(BattleSide.Opponent)];
    public SourceFieldRegisters Field { get; init; } = new();
    public WildBattleCalledBattleKind BattleKind { get; init; }
    public int Turn { get; init; }
    public long Revision { get; init; }
    public long EventSequence { get; init; }
    public RngState Rng { get; init; }
    public ushort RandomTurnNumber { get; init; }
    public WildBattleOutcome Outcome { get; init; }
    public int RunAttempts { get; init; }
    public byte SourceAbsentMask { get; init; }
    public byte TurnStartAbsentMask { get; init; }
    public byte DeferredKoVacancyMask { get; init; }
    public ImmutableArray<SourceQueuedAction> Queue { get; init; } = [];
    public int NextAction { get; init; }
    public ImmutableArray<SourceSwitchReservation> Reservations { get; init; } = [];
    public ImmutableArray<SourcePendingSwitch> PendingSwitches { get; init; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<SourcePreparedMedicine>? PreparedMedicines { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceWildEncounterContext? WildEncounter { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Game.Domain.Trainers.SourceTrainerEncounterContext? TrainerEncounter { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceTrainerTowerEncounterContext? TrainerTowerEncounter { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceOpponentFaintReceipt? LastOpponentFaint { get; init; }
    public SourceFourActionCursor? FourTurn { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceFourRewardState? Rewards { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceEntryAbilityState? EntryAbilities { get; init; }
    [JsonIgnore] public int BattlerCount => Layout == SourceBattleLayout.Singles ? 2 : 4;
    public SourceBattleSlot Get(BattlerSlot slot) => Slots[slot.Value];
    public SourceBattleParty Party(BattleSide side) => Parties[(int)side];
    public bool IsSourceAbsent(BattlerSlot slot) => (SourceAbsentMask & slot.Mask) != 0;
    public bool IsDeferredKoVacant(BattlerSlot slot) => (DeferredKoVacancyMask & slot.Mask) != 0;
    public bool CanAct(BattlerSlot slot) => slot.Value < BattlerCount && !IsSourceAbsent(slot) &&
        !IsDeferredKoVacant(slot) && Get(slot).Body?.Pokemon.CurrentHp > 0;
    public bool IsCurrent(OccupantToken token) => token.Slot.Value < BattlerCount && Get(token.Slot).Occupant == token;
}
