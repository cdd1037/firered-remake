using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal int HeldAccuracy(int accuracy, WildBattleCombatant target) => SourceHeldItemModifiers.Accuracy(catalog, target, accuracy);
}
