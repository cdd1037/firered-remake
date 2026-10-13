using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Domain.Items;

public static class SixIslandEscapeRopeRules
{
    public static bool IsDottedHole(MapId map) => DottedHoleSource.Maps.ContainsKey(map);
    private static bool IsDottedDestination(EscapeRopeDestination? d) => d is { MapId.Value: "map.six-island-ruin-valley", X: 24, Y: 25 };
    public static string? ValidateDestination(EscapeRopeDestination? d) => IsDottedDestination(d) ? null : FourIslandEscapeRopeRules.ValidateDestination(d);
    public static EscapeRopeDestination? RecordWarp(EscapeRopeDestination? previous, TilePosition source, MapId destination)
    {
        if (ValidateDestination(previous) is {} error) throw new ArgumentException(error, nameof(previous));
        if (source.MapId == DottedHoleSource.RuinValley && IsDottedHole(destination))
        {
            if (destination != DottedHoleSource.FirstFloor || source.X != 24 || source.Y != 24 || source.Facing != Direction.North)
                throw new NotSupportedException("Unverified Dotted Hole entrance.");
            return new(source.MapId, 24, 25);
        }
        return FourIslandEscapeRopeRules.RecordWarp(IsDottedDestination(previous) ? null : previous, source, destination) ?? previous;
    }
    public static EscapeRopeUseResult Use(SixIslandShopCatalog shop, InventoryState inventory, MapId map,
        EscapeRopeDestination? destination, PotionUseContext context)
    {
        EscapeRopeUseResult Fail(string error) => new(inventory, null, false, 0, error);
        if (shop.ValidateInventory(inventory) is {} error) return Fail(error);
        if (!IsDottedHole(map))
        {
            var old = FourIslandEscapeRopeRules.Use(shop.Predecessor, shop.InventoryForPredecessor(inventory), map,
                IsDottedDestination(destination) ? null : destination, context);
            return old.Succeeded ? old with { Inventory = shop.RestoreFromPredecessor(inventory, old.Inventory) } : Fail(old.Error!);
        }
        if (context != PotionUseContext.Field) return Fail("escape_rope_field_only");
        if (destination is null) return Fail("escape_rope_destination_missing");
        if (!IsDottedDestination(destination)) return Fail("escape_rope_destination_mismatch");
        var removed = EarlyInventoryAddRules.Remove(shop.Catalog, inventory, EscapeRopeRules.Item, 1);
        return removed.Succeeded ? new(removed.State, new(destination.MapId, 24, 25, 3, Direction.South), true, 1, null) : Fail(removed.Error!);
    }
}
