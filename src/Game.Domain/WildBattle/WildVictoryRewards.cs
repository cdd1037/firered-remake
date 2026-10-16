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
