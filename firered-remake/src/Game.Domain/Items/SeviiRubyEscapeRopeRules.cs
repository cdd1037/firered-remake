using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Domain.Items;

public static class SeviiRubyEscapeRopeRules
{
    public static bool IsRubyCave(MapId map) => map.Value is "map.mt-ember-ruby-path-1f" or "map.mt-ember-ruby-path-b1f" or
        "map.mt-ember-ruby-path-b2f" or "map.mt-ember-ruby-path-b3f" or "map.mt-ember-ruby-path-b4f" or "map.mt-ember-ruby-path-b5f" or
        "map.mt-ember-ruby-path-b1f-stairs" or "map.mt-ember-ruby-path-b2f-stairs";
    private static bool IsRubyDestination(EscapeRopeDestination? d) => d is { MapId.Value: "map.mt-ember-exterior", X: 42, Y: 40 };
    public static string? ValidateDestination(EscapeRopeDestination? d) => IsRubyDestination(d) ? null : VictoryRoadEscapeRopeRules.ValidateDestination(d);
    public static EscapeRopeDestination? RecordWarp(EscapeRopeDestination? previous, TilePosition source, MapId destination)
    {
        if (ValidateDestination(previous) is { } error) throw new ArgumentException(error, nameof(previous));
        if (source.MapId.Value == "map.mt-ember-exterior" && IsRubyCave(destination))
        {
            if ((destination.Value, source.X, source.Y, source.Facing) != ("map.mt-ember-ruby-path-1f", 42, 39, Direction.North))
                throw new NotSupportedException("Unverified Ruby cave entrance.");
            return new(source.MapId, 42, 40);
        }
        return VictoryRoadEscapeRopeRules.RecordWarp(IsRubyDestination(previous) ? null : previous, source, destination) ?? previous;
    }
    public static EscapeRopeUseResult Use(SeviiRubyShopCatalog shop, InventoryState inventory, MapId map,
        EscapeRopeDestination? destination, PotionUseContext context)
    {
        EscapeRopeUseResult Fail(string error) => new(inventory, null, false, 0, error);
        if (shop.ValidateInventory(inventory) is { } error) return Fail(error);
        if (!IsRubyCave(map))
        {
            var old = VictoryRoadEscapeRopeRules.Use(shop.Predecessor, shop.InventoryForPredecessor(inventory), map,
                IsRubyDestination(destination) ? null : destination, context);
            return old.Succeeded ? old with { Inventory = shop.RestoreFromPredecessor(inventory, old.Inventory) } : Fail(old.Error!);
        }
        if (context != PotionUseContext.Field) return Fail("escape_rope_field_only");
        if (destination is null) return Fail("escape_rope_destination_missing");
        if (!IsRubyDestination(destination)) return Fail("escape_rope_destination_mismatch");
        var removed = EarlyInventoryAddRules.Remove(shop.Catalog, inventory, EscapeRopeRules.Item, 1);
        return removed.Succeeded ? new(removed.State, new(destination.MapId, 42, 40, 3, Direction.South), true, 1, null) : Fail(removed.Error!);
    }
}
