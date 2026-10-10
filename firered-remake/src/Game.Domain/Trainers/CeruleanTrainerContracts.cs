using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

public sealed record CeruleanTrainerBattleDefinition(TrainerId Trainer, int Mode, int RivalFlags, uint BattleTypeFlags,
    string ScriptEntry, string IntroText, string DefeatText, string VictoryText, string PostBattleText,
    string EntryScript, string Callback, TrainerSourceRef Source)
{
    public string? PostBattleScript { get; init; }
    public string? MusicOption { get; init; }
}
public sealed record CeruleanTrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);
public sealed record CeruleanTrainerAiInput(WildBattleCombatant User, WildBattleCombatant Target,
    string? KnownTargetAbility = null, uint AiFlags = 1)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool TargetHasReserves { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool UserHasReserves { get; init; }
}
public sealed record CeruleanTrainerAiDecision(TransitionStatus Status, int? MoveSlot, ImmutableArray<int> Scores,
    ImmutableArray<int> SimulatedDamagePercent, RngState Rng, ImmutableArray<RandomTrace> Trace, Diagnostic? Diagnostic)
{
    public bool Succeeded => Status == TransitionStatus.Accepted;
    public ImmutableArray<ForestTrainerAbilityRead> AbilityReads { get; init; } = [];
    public ImmutableArray<int> PredictedDamage { get; init; } = [];
}
/// <summary>LastLandedMove/LastHitBy are source target-side history, not the last attempted move.
/// Null represents MOVE_NONE/0xFFFF and 0xFF respectively in this bounded singles profile.</summary>
public sealed record CeruleanTrainerSwitchInput(TrainerId Trainer, ImmutableArray<WildBattleCombatant> Party,
    int ActiveIndex, WildBattleCombatant User, WildBattleCombatant Target,
    MoveId? LastLandedMove = null, WildBattleSide? LastHitBy = null, int PlayerPartyIndex = 0);
public sealed record CeruleanTrainerSwitchDecision(TransitionStatus Status, int? PartyIndex,
    RngState Rng, ImmutableArray<RandomTrace> Trace, Diagnostic? Diagnostic)
{
    public bool Succeeded => Status == TransitionStatus.Accepted;
}
