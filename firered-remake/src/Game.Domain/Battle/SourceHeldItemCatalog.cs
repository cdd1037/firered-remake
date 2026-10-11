using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Domain.Battle;

/// <summary>Composition binds one authenticated item proof to the exact authenticated mechanics
/// catalog. The immutable item pack is shared with bag items; no authored copy of its table exists.</summary>
public static class SourceHeldItemCatalog
{
    private static readonly ConditionalWeakTable<PokemonCatalog, SourceBattleItemCatalog> Bound = new();
    public static void Bind(SourceMechanicsCatalog mechanics, SourceBattleItemCatalog items)
    {
        ArgumentNullException.ThrowIfNull(mechanics); ArgumentNullException.ThrowIfNull(items);
        lock (Bound)
        {
            if (Bound.TryGetValue(mechanics.Catalog, out var prior))
            { if (!ReferenceEquals(prior, items)) throw new ArgumentException("source_held_item_catalog_already_bound"); return; }
            Bound.Add(mechanics.Catalog, items);
        }
    }
    public static SourceBattleItemCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Bound.TryGetValue(catalog, out var items) ? items : null;
    internal static SourceBattleItemDefinition? Find(PokemonCatalog? catalog, string symbol) =>
        For(catalog)?.Find(new ItemId("pret:" + symbol));
    internal static bool Supported(PokemonCatalog? catalog, string symbol) => symbol != "ITEM_ENIGMA_BERRY" &&
        Find(catalog, symbol) is { } item && item.Pocket is not ("POCKET_KEY_ITEMS" or "POCKET_TM_CASE");
    internal static bool Consumable(PokemonCatalog? catalog, string symbol) =>
        Find(catalog, symbol) is { } item && SourceHeldBerryRules.IsConsumableEffect(item.HoldEffect);
}
