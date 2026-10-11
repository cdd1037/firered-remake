using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal virtual int HeldAccuracy(int accuracy, WildBattleCombatant target) => accuracy;
}
internal sealed partial class FrameRecipeContext
{
    internal override int HeldAccuracy(int accuracy, WildBattleCombatant target) => SourceHeldItemModifiers.Accuracy(catalog, target, accuracy);
}
