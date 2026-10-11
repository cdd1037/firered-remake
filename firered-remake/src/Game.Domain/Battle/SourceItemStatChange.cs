using Game.Domain.WildBattle;
namespace Game.Domain.Battle;

public sealed record SourceItemStatChange(WildBattleStat Stat, int Before, int After);
