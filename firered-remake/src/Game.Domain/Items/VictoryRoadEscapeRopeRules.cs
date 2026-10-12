using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Domain.Items;

/// <summary>The two original Route23 cave entrances extend the First-Sevii saved escape warp.
/// Call RecordWarp only after a successful actual warp; ordinary floor changes keep the record.</summary>
public static class VictoryRoadEscapeRopeRules
{
    public const string SourceCommit = EscapeRopeRules.SourceCommit;
    public const string RulesVersion = "firered-victory-road-escape-rope-v1";

    public static bool IsVictoryRoad(MapId map) => map.Value is
        "map.victory-road-1f" or "map.victory-road-2f" or "map.victory-road-3f";

    public static bool IsSupportedMap(MapId map) => IsVictoryRoad(map) ||
        map.Value == "map.three-island-berry-forest" || CinnabarEscapeRopeRules.IsSupportedMap(map);

    public static bool IsVictoryRoadDestination(EscapeRopeDestination? destination) =>
        destination is { MapId.Value: "map.route-23", X: 5 or 18, Y: 29 };

    public static string? ValidateDestination(EscapeRopeDestination? destination) =>
        IsVictoryRoadDestination(destination) ? null : FirstSeviiEscapeRopeRules.ValidateDestination(destination);

    public static EscapeRopeDestination? RecordWarp(EscapeRopeDestination? previous,
        TilePosition sourceWarp, MapId destinationMap)
    {
        ArgumentNullException.ThrowIfNull(sourceWarp);
        if (ValidateDestination(previous) is { } error) throw new ArgumentException(error, nameof(previous));
        if (sourceWarp.MapId.Value == "map.route-23" && IsVictoryRoad(destinationMap))
        {
            bool originalEntrance = (destinationMap.Value, sourceWarp.X, sourceWarp.Y,
                sourceWarp.Elevation, sourceWarp.Facing) is
                ("map.victory-road-1f", 5, 28, 3, Direction.North) or
                ("map.victory-road-2f", 18, 28, 3, Direction.North);
            if (!originalEntrance) throw new NotSupportedException("Unverified Victory Road Escape Rope entrance.");
            // Route23 warp0/1, MB_CAVE_DOOR. overworld.c UpdateEscapeWarp adds one to y
            // for the original north-facing entrance; the saved record is a coordinate warp.
            return new(sourceWarp.MapId, sourceWarp.X, 29);
        }
        var inherited = FirstSeviiEscapeRopeRules.RecordWarp(
            IsVictoryRoadDestination(previous) ? null : previous, sourceWarp, destinationMap);
        return inherited ?? previous;
    }

    /// <summary>Uses the authenticated successor inventory, projecting and restoring its two new
    /// items internally. Application commits Inventory and pending Arrival once, before message acknowledgement.</summary>
    public static EscapeRopeUseResult Use(VictoryRoadShopCatalog shop, InventoryState inventory,
        MapId currentMap, EscapeRopeDestination? destination, PotionUseContext context)
    {
        ArgumentNullException.ThrowIfNull(shop);
        ArgumentNullException.ThrowIfNull(inventory);
        EscapeRopeUseResult Fail(string error) => new(inventory, null, false, 0, error);
        if (shop.ValidateInventory(inventory) is { } error) return Fail(error);
        if (!IsVictoryRoad(currentMap))
        {
            var inherited = FirstSeviiEscapeRopeRules.Use(shop.Predecessor,
                shop.InventoryForPredecessor(inventory), currentMap,
                IsVictoryRoadDestination(destination) ? null : destination, context);
            return inherited.Succeeded
                ? inherited with { Inventory = RestoreConsumedRope(shop, inventory, inherited.Inventory) }
                : Fail(inherited.Error!);
        }
        if (context != PotionUseContext.Field) return Fail("escape_rope_field_only");
        if (destination is null) return Fail("escape_rope_destination_missing");
        if (ValidateDestination(destination) is { } invalid) return Fail(invalid);
        if (!IsVictoryRoadDestination(destination)) return Fail("escape_rope_destination_mismatch");
        if (OpeningInventoryRules.Quantity(inventory.BagItems, EscapeRopeRules.Item) == 0)
            return Fail("escape_rope_not_in_bag");
        var removed = EarlyInventoryAddRules.Remove(shop.Predecessor.Catalog,
            shop.InventoryForPredecessor(inventory), EscapeRopeRules.Item, 1);
        if (!removed.Succeeded) return Fail(removed.Error!);
        // Source Route23 arrivals (5,29)=0x3282 and (18,29)=0x3009: collision0,
        // elevation3, MB_NORMAL; GetAdjustedInitialDirection returns DIR_SOUTH.
        return new(RestoreConsumedRope(shop, inventory, removed.State),
            new(destination.MapId, destination.X, destination.Y, 3, Direction.South), true, 1, null);
    }

    private static InventoryState RestoreConsumedRope(VictoryRoadShopCatalog shop,
        InventoryState original, InventoryState changed)
    {
        var restored = shop.RestoreFromPredecessor(original, changed);
        // This operation only removes a rope. If its last unit preceded Guard Spec.,
        // source bag compaction must retain every surviving stack's relative order.
        var order = original.BagItems.Select((stack, index) => (stack.Item, index))
            .ToDictionary(entry => entry.Item, entry => entry.index);
        return restored with { BagItems = restored.BagItems.OrderBy(stack => order[stack.Item]).ToImmutableArray() };
    }
}
