using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Domain.Items;

/// <summary>Source Mansion entrance extends the existing saved escapeWarp mechanism; no inferred destinations.</summary>
public static class CinnabarEscapeRopeRules
{
    public const string RulesVersion = "firered-cinnabar-escape-rope-v1";
    public static bool IsMansion(MapId map) => map.Value is "map.pokemon-mansion-1f" or
        "map.pokemon-mansion-2f" or "map.pokemon-mansion-3f" or "map.pokemon-mansion-b1f";
    public static bool IsSupportedMap(MapId map) => IsMansion(map) ||
        EscapeRopeRules.IsSupportedMap(map, route11Rules: true, rockTunnelRules: true, pokemonTowerRules: true, silphRules: true);
    private static bool IsMansionDestination(EscapeRopeDestination? destination) =>
        destination is { MapId.Value: "map.cinnabar-island", X: 8, Y: 4 };
    public static string? ValidateDestination(EscapeRopeDestination? destination) => IsMansionDestination(destination) ? null :
        EscapeRopeRules.ValidateDestination(destination, route11Rules: true, rockTunnelRules: true, pokemonTowerRules: true, silphRules: true);

    public static EscapeRopeDestination? RecordWarp(EscapeRopeDestination? previous, TilePosition sourceWarp, MapId destinationMap)
    {
        ArgumentNullException.ThrowIfNull(sourceWarp);
        if (ValidateDestination(previous) is { } error) throw new ArgumentException(error, nameof(previous));
        if (sourceWarp.MapId.Value == "map.cinnabar-island" && destinationMap.Value == "map.pokemon-mansion-1f")
        {
            // Pinned data/maps/CinnabarIsland/map.json warp0; overworld.c UpdateEscapeWarp.
            if (sourceWarp is not { X: 8, Y: 3, Elevation: 3, Facing: Direction.North })
                throw new NotSupportedException("Unverified Mansion Escape Rope entrance.");
            return new(sourceWarp.MapId, 8, 4);
        }
        // Historical validator receives only destinations its unchanged proof admits.
        var old = EscapeRopeRules.RecordWarp(IsMansionDestination(previous) ? null : previous, sourceWarp, destinationMap,
            route11Rules: true, rockTunnelRules: true, pokemonTowerRules: true, silphRules: true);
        return old ?? previous;
    }

    public static EscapeRopeUseResult Use(CinnabarShopCatalog shop, InventoryState inventory,
        MapId currentMap, EscapeRopeDestination? destination, PotionUseContext context)
    {
        ArgumentNullException.ThrowIfNull(shop); ArgumentNullException.ThrowIfNull(inventory);
        EscapeRopeUseResult Fail(string error) => new(inventory, null, false, 0, error);
        if (shop.ValidateInventory(inventory) is { } error) return Fail(error);
        if (!IsMansion(currentMap))
        {
            var old = EscapeRopeRules.Use(shop.Predecessor.Catalog, shop.InventoryForPredecessor(inventory), currentMap, destination, context,
                route11Rules: true, rockTunnelRules: true, pokemonTowerRules: true, silphRules: true);
            return old.Succeeded ? old with { Inventory = shop.RestoreFromPredecessor(inventory, old.Inventory) } : Fail(old.Error!);
        }
        if (context != PotionUseContext.Field) return Fail("escape_rope_field_only");
        if (destination is null) return Fail("escape_rope_destination_missing");
        if (ValidateDestination(destination) is { } invalid) return Fail(invalid);
        if (!IsMansionDestination(destination)) return Fail("escape_rope_destination_mismatch");
        int quantity = OpeningInventoryRules.Quantity(inventory.BagItems, EscapeRopeRules.Item);
        if (quantity == 0) return Fail("escape_rope_not_in_bag");
        var definition = shop.Catalog.Items.Single(i => i.Id == EscapeRopeRules.Item);
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, quantity - 1),
            new(destination.MapId, destination.X, destination.Y, 3, Direction.South), true, 1, null);
    }
}
