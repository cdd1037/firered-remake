using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Domain.Battle;

/// <summary>Held-item queries over the catalog's explicitly composed immutable item data.</summary>
public static class SourceHeldItemCatalog
{
    internal static SourceBattleItemDefinition? Find(PokemonCatalog? catalog, string symbol) =>
        catalog?.BattleItems?.Find(new ItemId("pret:" + symbol));
    internal static bool Supported(PokemonCatalog? catalog, string symbol) => symbol != "ITEM_ENIGMA_BERRY" &&
        Find(catalog, symbol) is { } item && item.Pocket is not ("POCKET_KEY_ITEMS" or "POCKET_TM_CASE");
    internal static bool Consumable(PokemonCatalog? catalog, string symbol) =>
        Find(catalog, symbol) is { } item && SourceHeldBerryRules.IsConsumableEffect(item.HoldEffect);
}
