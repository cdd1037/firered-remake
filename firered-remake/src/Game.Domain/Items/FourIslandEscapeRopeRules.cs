using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Domain.Items;

public static class FourIslandEscapeRopeRules
{
    public static bool IsIcefallCave(MapId map) => map.Value is "map.four-island-icefall-cave-entrance" or
        "map.four-island-icefall-cave-1f" or "map.four-island-icefall-cave-b1f" or "map.four-island-icefall-cave-back";
    private static bool IsIcefallDestination(EscapeRopeDestination? d) => d is { MapId.Value: "map.four-island", X: 38, Y: 13 };
    public static string? ValidateDestination(EscapeRopeDestination? d) => IsIcefallDestination(d) ? null : SeviiRubyEscapeRopeRules.ValidateDestination(d);
    public static EscapeRopeDestination? RecordWarp(EscapeRopeDestination? previous, TilePosition source, MapId destination)
    {
        if (ValidateDestination(previous) is { } error) throw new ArgumentException(error, nameof(previous));
        if (source.MapId.Value == "map.four-island" && IsIcefallCave(destination))
        {
            if ((destination.Value, source.X, source.Y, source.Facing) != ("map.four-island-icefall-cave-entrance", 38, 12, Direction.North))
                throw new NotSupportedException("Unverified Icefall cave entrance.");
            return new(source.MapId, 38, 13);
        }
        return SeviiRubyEscapeRopeRules.RecordWarp(IsIcefallDestination(previous) ? null : previous, source, destination) ?? previous;
    }
    public static EscapeRopeUseResult Use(FourIslandShopCatalog shop, InventoryState inventory, MapId map,
        EscapeRopeDestination? destination, PotionUseContext context)
    {
        EscapeRopeUseResult Fail(string error) => new(inventory, null, false, 0, error);
        if (shop.ValidateInventory(inventory) is { } error) return Fail(error);
        if (!IsIcefallCave(map))
        {
            var old = SeviiRubyEscapeRopeRules.Use(shop.Predecessor, shop.InventoryForPredecessor(inventory), map,
                IsIcefallDestination(destination) ? null : destination, context);
            return old.Succeeded ? old with { Inventory = shop.RestoreFromPredecessor(inventory, old.Inventory) } : Fail(old.Error!);
        }
        if (context != PotionUseContext.Field) return Fail("escape_rope_field_only");
        if (destination is null) return Fail("escape_rope_destination_missing");
        if (!IsIcefallDestination(destination)) return Fail("escape_rope_destination_mismatch");
        var removed = EarlyInventoryAddRules.Remove(shop.Catalog, inventory, EscapeRopeRules.Item, 1);
        return removed.Succeeded ? new(removed.State, new(destination.MapId, 38, 13, 3, Direction.South), true, 1, null) : Fail(removed.Error!);
    }
}
