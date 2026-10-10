using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.Items;

/// <summary>Original Net/Nest Ball multiplier in tenths, before catch-rate division and HP/status odds.
/// This helper does not admit a capture action, spend a ball, draw RNG or mutate a Pokemon.</summary>
public static class RocketHideoutBallRules
{
    public static int Multiplier(ItemId ball, PokemonSpeciesDefinition species, int level)
    {
        ArgumentNullException.ThrowIfNull(species);
        if (level is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(level));
        if (ball == RocketHideoutItemRules.NetBallId)
            return species.Type1 is "TYPE_WATER" or "TYPE_BUG" || species.Type2 is "TYPE_WATER" or "TYPE_BUG" ? 30 : 10;
        if (ball == RocketHideoutItemRules.NestBallId) return level < 40 ? Math.Max(10, 40 - level) : 10;
        throw new NotSupportedException("Only original Net Ball and Nest Ball multiplier rules are admitted.");
    }
}
