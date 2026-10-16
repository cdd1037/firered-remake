using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;

public sealed record WildBattleYawnEvent(long Sequence, int Turn, WildBattleSide Side,
    int Before, int After, string Reason) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleSleepTalkEvent(long Sequence, int Turn, WildBattleSide Side,
    int CalledSlot, MoveId CalledMove) : WildBattleEvent(Sequence, Turn);
