using Game.Domain.Ids;
using Game.Domain.Battle;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>Cmd_recoverbasedonsunlight arithmetic, independent of weather storage.</summary>
public static class WildBattleSourceRecovery
{
    public static int SunlightAmount(int maximumHp, bool weatherPresent, bool sunny, bool weatherSuppressed)
    {
        if (maximumHp <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHp));
        return Math.Max(1, !weatherPresent || weatherSuppressed ? maximumHp / 2 : sunny ? 20 * maximumHp / 30 : maximumHp / 4);
    }

    /// <summary>Cmd_maxattackhalvehp returns no HP debit on either failed condition.</summary>
    public static int BellyDrumCost(int maximumHp, int currentHp, int attackStage)
    {
        if (maximumHp <= 0 || currentHp < 0 || currentHp > maximumHp || attackStage is < 0 or > 12)
            throw new ArgumentOutOfRangeException(nameof(maximumHp));
        int half = Math.Max(1, maximumHp / 2);
        return attackStage < 12 && currentHp > half ? half : 0;
    }
}

public static partial class WildBattleRules
{
    private static bool HasStatRecoveryRecipe(string effect) => SourceRecipeContext.HasStatRecoveryRecipe(effect);
    static partial void AddCalledStatRecoverySupport(PokemonMoveDefinition move, ref bool supported)
    {
        if (MetronomeSourceMoveDefinition.Matches(move) && HasStatRecoveryRecipe(move.Effect)) supported = true;
    }
    internal sealed partial class Execution
    {
        partial void ExecuteCalledStatRecoveryEffect(PokemonCatalog catalog, WildBattleSide side, int slot,
            PokemonMoveDefinition move, string purpose, ref bool handled) => Recipes.ExecuteStatRecovery(catalog, S(side), slot, move, purpose, ref handled);
        private void SourceStatRecoveryHeal(WildBattleSide side, PokemonMoveDefinition move, int amount) => Recipes.SourceStatRecoveryHeal(S(side), move, amount);
        private void SourceChangeStat(WildBattleSide side, WildBattleSide target, WildBattleStat stat, int amount, MoveId move,
            ref bool preventionShown, bool secondary = false, bool certain = false) =>
            Recipes.SourceChangeStat(S(side), S(target), stat, amount, move, ref preventionShown, secondary, certain);
    }
}
