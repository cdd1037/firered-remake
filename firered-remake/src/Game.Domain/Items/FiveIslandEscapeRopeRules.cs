using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Domain.Items;

public static class FiveIslandEscapeRopeRules
{
    public static bool IsWarehouse(MapId map) => map.Value == "map.five-island-rocket-warehouse";
    public static bool IsPowerPlant(MapId map) => map.Value == "map.power-plant";
    private static bool IsPowerPlantDestination(EscapeRopeDestination? d) => d is
        { MapId.Value: "map.route-10", X: 7, Y: 41 } or { MapId.Value: "map.route-10", X: 2, Y: 38 };
    private static bool IsWarehouseDestination(EscapeRopeDestination? d) => d is { MapId.Value: "map.five-island-meadow", X: 12, Y: 22 };
    public static string? ValidateDestination(EscapeRopeDestination? d) => IsWarehouseDestination(d) || IsPowerPlantDestination(d) ? null : SixIslandEscapeRopeRules.ValidateDestination(d);
    public static EscapeRopeDestination? RecordWarp(EscapeRopeDestination? previous, TilePosition source, MapId destination)
    {
        if (ValidateDestination(previous) is {} error) throw new ArgumentException(error, nameof(previous));
        if (source.MapId.Value == "map.route-10" && IsPowerPlant(destination))
        {
            // Source Route10 warp2 is a cave door; warp4 is an east-arrow entrance.
            if (source.Elevation != 3 || (source.X, source.Y, source.Facing) is not
                ((7, 40, Direction.North) or (2, 37, Direction.East)))
                throw new NotSupportedException("Unverified Power Plant entrance.");
            return new(source.MapId, source.X, source.Y + 1);
        }
        if (source.MapId.Value == "map.five-island-meadow" && IsWarehouse(destination))
        {
            if (destination.Value != "map.five-island-rocket-warehouse" || source.X != 12 || source.Y != 21 || source.Facing != Direction.North)
                throw new NotSupportedException("Unverified Rocket Warehouse entrance.");
            return new(source.MapId, 12, 22);
        }
        return SixIslandEscapeRopeRules.RecordWarp(IsWarehouseDestination(previous) || IsPowerPlantDestination(previous) ? null : previous, source, destination) ?? previous;
    }
    public static EscapeRopeUseResult Use(FiveIslandShopCatalog shop, InventoryState inventory, MapId map,
        EscapeRopeDestination? destination, PotionUseContext context)
    {
        EscapeRopeUseResult Fail(string error) => new(inventory, null, false, 0, error);
        if (shop.ValidateInventory(inventory) is {} error) return Fail(error);
        if (!IsWarehouse(map) && !IsPowerPlant(map))
        {
            var old = SixIslandEscapeRopeRules.Use(shop.Predecessor, shop.InventoryForPredecessor(inventory), map,
                IsWarehouseDestination(destination) || IsPowerPlantDestination(destination) ? null : destination, context);
            return old.Succeeded ? old with { Inventory = shop.RestoreFromPredecessor(inventory, old.Inventory) } : Fail(old.Error!);
        }
        if (context != PotionUseContext.Field) return Fail("escape_rope_field_only");
        if (destination is null) return Fail("escape_rope_destination_missing");
        if (!(IsPowerPlant(map) ? IsPowerPlantDestination(destination) : IsWarehouseDestination(destination))) return Fail("escape_rope_destination_mismatch");
        var removed = EarlyInventoryAddRules.Remove(shop.Catalog, inventory, EscapeRopeRules.Item, 1);
        return removed.Succeeded ? new(removed.State, new(destination.MapId, destination.X, destination.Y, 3, Direction.South), true, 1, null) : Fail(removed.Error!);
    }
}
