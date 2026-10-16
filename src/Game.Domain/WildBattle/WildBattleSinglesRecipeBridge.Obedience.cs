using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

public sealed record WildBattleObedienceEvent(long Sequence, int Turn, WildBattleSide Actor, MoveId AttemptedMove,
    SourceObedienceDecision Decision) : WildBattleEvent(Sequence, Turn);
