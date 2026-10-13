using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(WildRewardExperienceEvent), "experience")]
[JsonDerivedType(typeof(WildRewardEvEvent), "evs")]
[JsonDerivedType(typeof(WildRewardLevelEvent), "level")]
[JsonDerivedType(typeof(WildRewardFriendshipEvent), "friendship")]
[JsonDerivedType(typeof(WildRewardMoveLearnedEvent), "move-learned")]
[JsonDerivedType(typeof(WildRewardMovePendingEvent), "move-pending")]
public abstract record WildRewardEvent;
public sealed record WildRewardExperienceEvent(uint Before, uint After, int Gained) : WildRewardEvent;
public sealed record WildRewardEvEvent(SixStats Before, SixStats After, SixStats Gained) : WildRewardEvent;
public sealed record WildRewardLevelEvent(int Before, int After, BattleStats StatsBefore, BattleStats StatsAfter,
    int HpBefore, int HpAfter) : WildRewardEvent;
public sealed record WildRewardFriendshipEvent(int Before, int After) : WildRewardEvent;
public sealed record WildRewardMoveLearnedEvent(int Level, MoveId Move, int Slot) : WildRewardEvent;
public sealed record WildRewardMovePendingEvent(int Level, MoveId Move) : WildRewardEvent;
public sealed record WildPendingMove(int Level, MoveId Move);

/// <summary>
/// Unhealed reward state. PendingMoves must be retained and resolved in order before returning to
/// the field; later candidates may become AlreadyKnown after the earlier replacement decisions.
/// ExperienceGained is the source announcement; ExperienceCredited can be smaller at level 100.
/// </summary>
public sealed record WildVictoryRewardResult(PartyPokemon Pokemon, int ExperienceGained, SixStats EvsGained,
    int LevelsGained, int FriendshipDelta, int HpDelta, ImmutableArray<WildRewardEvent> Events,
    ImmutableArray<WildPendingMove> PendingMoves)
{
    public int ExperienceCredited => Events.OfType<WildRewardExperienceEvent>().Sum(e => checked((int)(e.After - e.Before)));
}
