using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Domain.Items;

public sealed record EscapeRopeDestination(MapId MapId, int X, int Y);
public sealed record EscapeRopeUseResult(InventoryState Inventory, TilePosition? Arrival,
    bool Succeeded, int ItemsConsumed, string? Error);
public sealed record EscapeRopeEntrance(TilePosition Source, MapId DestinationMap, EscapeRopeDestination Exit);

/// <summary>Actual dungeon membership, entrance transitions and saved exits of the composed game.</summary>
public sealed class EscapeRopePolicy
{
    public ImmutableDictionary<MapId, ImmutableArray<EscapeRopeDestination>> Exits { get; }
    public ImmutableArray<EscapeRopeEntrance> Entrances { get; }
    public EscapeRopePolicy(ImmutableDictionary<MapId, ImmutableArray<EscapeRopeDestination>> exits,
        ImmutableArray<EscapeRopeEntrance> entrances)
    { Exits = exits; Entrances = entrances; }
    public string? ValidateDestination(EscapeRopeDestination? destination) => destination is null ||
        Exits.Values.Any(values => values.Contains(destination)) ? null : "escape_rope_destination_invalid";
    public EscapeRopeDestination? RecordWarp(EscapeRopeDestination? previous, TilePosition source, MapId destination)
    {
        if (ValidateDestination(previous) is {} error) throw new ArgumentException(error, nameof(previous));
        if (!StableId.IsValid(source.MapId.Value) || !StableId.IsValid(destination.Value) ||
            source.X < 0 || source.Y < 0 || source.Elevation is < 0 or > 15 || !Enum.IsDefined(source.Facing))
            throw new ArgumentException("Invalid warp source position.", nameof(source));
        var entrances = Entrances.Where(e => e.Source.MapId == source.MapId && e.DestinationMap == destination).ToArray();
        if (entrances.Length == 0) return previous;
        return entrances.FirstOrDefault(e => e.Source == source)?.Exit
            ?? throw new NotSupportedException("Escape Rope entrance coordinates or direction are invalid.");
    }
}

/// <summary>One atomic item operation, independent of regional shop construction history.</summary>
public static class EscapeRopeRules
{
    public static ItemId Item { get; } = new("pret:ITEM_ESCAPE_ROPE");
    public static EscapeRopeDestination ForestDestination { get; } = new(new("map.three-island-bond-bridge"), 12, 6);
    public static EscapeRopeUseResult Use(InventoryCatalog items, EscapeRopePolicy policy,
        InventoryState inventory, MapId currentMap, EscapeRopeDestination? destination, PotionUseContext context)
    {
        EscapeRopeUseResult Fail(string error) => new(inventory, null, false, 0, error);
        if (items.Validate(inventory) is {} error) return Fail(error);
        if (context != PotionUseContext.Field) return Fail("escape_rope_field_only");
        if (!policy.Exits.TryGetValue(currentMap, out var exits)) return Fail("escape_rope_map_not_allowed");
        if (destination is null) return Fail("escape_rope_destination_missing");
        if (policy.ValidateDestination(destination) is {} invalid) return Fail(invalid);
        if (!exits.Contains(destination)) return Fail("escape_rope_destination_mismatch");
        if (!items.Items.TryGetValue(Item, out var definition) || definition.Pocket != "POCKET_ITEMS" ||
            definition.FieldUseFunction != "ItemUseOutOfBattle_EscapeRope") return Fail("escape_rope_item_unsupported");
        int quantity = OpeningInventoryRules.Quantity(inventory.BagItems, Item);
        if (quantity == 0) return Fail("escape_rope_not_in_bag");
        var changed = inventory with { BagItems = OpeningInventoryRules.SetQuantity(inventory.BagItems, Item, quantity - 1) };
        var arrival = new TilePosition(destination.MapId, destination.X, destination.Y, 3,
            destination.MapId.Value == "map.route-2" && destination.Y == 13 ? Direction.North : Direction.South);
        return new(changed, arrival, true, 1, null);
    }
}
