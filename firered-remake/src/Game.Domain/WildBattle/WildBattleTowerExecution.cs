using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;

public sealed record WildBattleTowerVolatileEvent(long Sequence, int Turn, WildBattleSide Side,
    string Effect, bool Applied, string Reason) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleSpiteEvent(long Sequence, int Turn, WildBattleSide Actor, WildBattleSide Target,
    MoveId Move, int Slot, int PpBefore, int PpAfter, int RolledReduction) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleTowerHpEvent(long Sequence, int Turn, WildBattleSide Side,
    string Effect, int HpBefore, int HpAfter, int Damage) : WildBattleEvent(Sequence, Turn);
