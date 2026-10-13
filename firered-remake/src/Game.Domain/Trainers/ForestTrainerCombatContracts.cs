using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

public enum ForestTrainerBattleStyle { Shift, Set }
public enum ForestTrainerCombatPhase { Ready, AwaitingExperience, AwaitingPlayerReplacement, AwaitingShiftChoice, Ended }

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

public sealed record CeruleanTrainerItemUsedEvent(long Sequence, int Turn, TrainerId Trainer, string Item, int PartyIndex, int HpBefore, int HpAfter) : ForestTrainerCombatEvent(Sequence, Turn);

public sealed record SsAnneTrainerPlayerForcedOutEvent(long Sequence, int Turn, int PreviousIndex, int PartyIndex,
    WildBattleCombatant Previous, WildBattleCombatant Replacement) : ForestTrainerCombatEvent(Sequence, Turn);
