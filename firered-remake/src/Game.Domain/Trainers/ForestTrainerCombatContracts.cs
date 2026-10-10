using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

public enum ForestTrainerBattleStyle { Shift, Set }
public enum ForestTrainerCombatPhase { Ready, AwaitingExperience, AwaitingPlayerReplacement, AwaitingShiftChoice, Ended }

/// <summary>Only Battle is the current pair. OpponentParty mirrors its active slot after every commit.
/// EXP, owned player party/inventory, participation and prize money remain application transactions.</summary>
public sealed record ForestTrainerCombatState(TrainerId Trainer, WildBattleState Battle,
    ImmutableArray<WildBattleCombatant> OpponentParty, int OpponentIndex,
    ForestTrainerCombatPhase Phase = ForestTrainerCombatPhase.Ready,
    ForestTrainerBattleStyle BattleStyle = ForestTrainerBattleStyle.Shift,
    bool HasPlayerReserves = false, bool ResidualsFinished = false, bool PlayerFaintedThisTurn = false,
    int? PendingOpponentIndex = null, string? KnownPlayerAbility = null, MoveId StaleCurrentMove = default,
    int ExperienceCompletedMask = 0, string RulesVersion = ForestTrainerCombatRules.RulesVersion)
{
    /// <summary>Explicit opt-in to the shared source action/party/turn contract; absent for historical profiles.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? SourceMoveRulesVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceTrainerTurnState? SourceTurn { get; init; }
    /// <summary>Full party slot order; null marks an egg/empty slot. Application refreshes before each command.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ImmutableArray<WildBattleCombatant?>? SsAnnePlayerParty { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? CeruleanPendingSwitchIndex { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int CeruleanPlayerPartyIndex { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int CeruleanItemsUsedMask { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SsAnneTrainerPlayerForcedOutEvent), "ssanne-player-forced-out")]
[JsonDerivedType(typeof(ForestTrainerCombatStartedEvent), "trainer-started")]
[JsonDerivedType(typeof(CeruleanTrainerItemUsedEvent), "trainer-item-used")]
[JsonDerivedType(typeof(ForestTrainerAiChosenEvent), "trainer-ai-chosen")]
[JsonDerivedType(typeof(ForestTrainerExperienceRequiredEvent), "trainer-experience-required")]
[JsonDerivedType(typeof(ForestTrainerExperienceCompletedEvent), "trainer-experience-completed")]
[JsonDerivedType(typeof(ForestTrainerOpponentSelectedEvent), "trainer-opponent-selected")]
[JsonDerivedType(typeof(ForestTrainerShiftOfferedEvent), "trainer-shift-offered")]
[JsonDerivedType(typeof(ForestTrainerOpponentReplacedEvent), "trainer-opponent-replaced")]
public abstract record ForestTrainerCombatEvent(long Sequence, int Turn);
public sealed record ForestTrainerCombatStartedEvent(long Sequence, int Turn, TrainerId Trainer,
    int PartySize) : ForestTrainerCombatEvent(Sequence, Turn);
public sealed record ForestTrainerAiChosenEvent(long Sequence, int Turn, int MoveSlot,
    ImmutableArray<int> Scores) : ForestTrainerCombatEvent(Sequence, Turn);
public sealed record ForestTrainerExperienceRequiredEvent(long Sequence, int Turn,
    int OpponentIndex, PokemonInstance FaintedOpponent) : ForestTrainerCombatEvent(Sequence, Turn);
public sealed record ForestTrainerExperienceCompletedEvent(long Sequence, int Turn,
    int OpponentIndex) : ForestTrainerCombatEvent(Sequence, Turn);
public sealed record ForestTrainerOpponentSelectedEvent(long Sequence, int Turn,
    int OpponentIndex, PokemonInstance Pokemon) : ForestTrainerCombatEvent(Sequence, Turn);
public sealed record ForestTrainerShiftOfferedEvent(long Sequence, int Turn,
    int OpponentIndex, PokemonInstance Pokemon) : ForestTrainerCombatEvent(Sequence, Turn);
public sealed record ForestTrainerOpponentReplacedEvent(long Sequence, int Turn, int PreviousIndex,
    int OpponentIndex, WildBattleCombatant Previous, WildBattleCombatant Replacement) : ForestTrainerCombatEvent(Sequence, Turn);

/// <summary>Both event arrays share Battle.EventSequence. Merge by Sequence for presentation.
/// Faults return the identical input state with empty arrays and no AI decision.</summary>
public sealed record ForestTrainerCombatResult(TransitionStatus Status, ForestTrainerCombatState? State,
    ImmutableArray<WildBattleEvent> Events, ImmutableArray<ForestTrainerCombatEvent> TrainerEvents,
    ImmutableArray<RandomTrace> Trace, Diagnostic? Diagnostic)
{
    public bool Succeeded => Status == TransitionStatus.Accepted;
    public string? Fault => Diagnostic?.Code;
    public ForestTrainerAiDecision? LastAiDecision { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PewterTrainerAiDecision? LastPewterAiDecision { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Route22TrainerAiDecision? LastRoute22AiDecision { get; init; }
}

public sealed record CeruleanTrainerItemUsedEvent(long Sequence, int Turn, TrainerId Trainer, string Item, int PartyIndex, int HpBefore, int HpAfter) : ForestTrainerCombatEvent(Sequence, Turn);

public sealed record SsAnneTrainerPlayerForcedOutEvent(long Sequence, int Turn, int PreviousIndex, int PartyIndex,
    WildBattleCombatant Previous, WildBattleCombatant Replacement) : ForestTrainerCombatEvent(Sequence, Turn);
