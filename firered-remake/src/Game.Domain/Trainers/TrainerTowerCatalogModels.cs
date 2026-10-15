using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Trainers;

public enum TrainerTowerChallengeKind { Single = 0, Double = 1, Knockout = 2, Mixed = 3 }
public enum TrainerTowerBattleFormat { Single = 0, Double = 1, Knockout = 2 }

public sealed record TrainerTowerWord(string Symbol, int Value, string Text);
public sealed record TrainerTowerPokemonTemplate(int PoolIndex, SpeciesId Species, int SourceSpeciesId,
    string HeldItem, int SourceHeldItemId, ImmutableArray<MoveId> Moves, int PpBonuses,
    SixStats Ivs, SixStats Evs, uint OriginalTrainerId, uint Personality, int AbilitySlot,
    string AbilityId, string Nickname, int Friendship);
public sealed record TrainerTowerTrainerTemplate(int Index, string Name, string FacilityClass,
    int SourceFacilityClass, string TrainerClass, int SourceTrainerClass, string TrainerPic,
    int SourceTrainerPic, int TextColor, ImmutableArray<TrainerTowerWord> SpeechBefore,
    ImmutableArray<TrainerTowerWord> SpeechWin, ImmutableArray<TrainerTowerWord> SpeechLose,
    ImmutableArray<TrainerTowerWord> SpeechAfter, ImmutableArray<TrainerTowerPokemonTemplate> Mons);
public sealed record TrainerTowerFloorTemplate(string Symbol, int SourceId, int EncodedFloorCount,
    TrainerTowerBattleFormat Format, int PrizeIndex, string PrizeItem,
    ImmutableArray<TrainerTowerTrainerTemplate> Trainers);
public sealed record TrainerTowerSelection(int TrainerIndex, int PoolIndex);
public sealed record TrainerTowerFloor(int Index, string TemplateSymbol, TrainerTowerBattleFormat Format,
    ImmutableArray<TrainerTowerSelection> Selection);
public sealed record TrainerTowerCourse(TrainerTowerChallengeKind Kind, string PrizeItem,
    int SourcePrizeItemId, ImmutableArray<TrainerTowerFloor> Floors);
public sealed record TrainerTowerLocalData(int LocalSetId, bool ExternalEReaderEnabled,
    ImmutableArray<TrainerTowerCourse> Courses, ImmutableArray<TrainerTowerFloorTemplate> Templates);
public sealed record TrainerTowerSelectedMember(int TrainerIndex, int PoolIndex,
    TrainerTowerTrainerTemplate Trainer, TrainerTowerPokemonTemplate Template);
public sealed record TrainerTowerBattleDescriptor(TrainerTowerChallengeKind Course, int FloorIndex,
    int TrainerIndex, TrainerTowerBattleFormat Format, int Level, string TemplateSymbol,
    ImmutableArray<TrainerTowerSelectedMember> Members, uint BattleTypeFlags, uint AiFlags);
