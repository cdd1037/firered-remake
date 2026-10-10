using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Domain.Items;

/// <summary>The source escapeWarp coordinate destination (warpId is always -1 in this bounded profile).
/// Facing and elevation belong to the destination map, not this saved record.</summary>
public sealed record EscapeRopeDestination(MapId MapId, int X, int Y);
public sealed record EscapeRopeUseResult(InventoryState Inventory, TilePosition? Arrival,
    bool Succeeded, int ItemsConsumed, string? Error);

/// <summary>Fixed-source Escape Rope for the three Mt. Moon floors and Viridian Forest.
/// Track actual cave/gate entrances; never infer an entrance from the current floor or nearest exit.</summary>
public static class EscapeRopeRules
{
    public const string SourceCommit = OpeningInventoryRules.SourceCommit;
    public const string RulesVersion = "firered-mtmoon-forest-escape-rope-v1";
    public static ItemId Item { get; } = new("pret:ITEM_ESCAPE_ROPE");

    private static bool IsDiglettMap(MapId map) => map.Value is
        "map.digletts-cave-south-entrance" or "map.digletts-cave-b1f" or "map.digletts-cave-north-entrance";
    private static bool IsRockMap(MapId map) => map.Value is "map.rock-tunnel-1f" or "map.rock-tunnel-b1f";
    public static bool IsSupportedMap(MapId map, bool route11Rules = false, bool rockTunnelRules = false) =>
        map.Value is "map.mt-moon-1f" or "map.mt-moon-b1f" or "map.mt-moon-b2f" or "map.viridian-forest" ||
        (route11Rules || rockTunnelRules) && IsDiglettMap(map) || rockTunnelRules && IsRockMap(map);

    /// <summary>Call once, only after a successful actual warp, with the source warp tile and its
    /// triggering facing, in local map coordinates (no GBA seven-tile padding).
    /// This deliberately tracks only the entrances needed by the admitted Escape Rope maps.
    /// Other warps, including Forest-to-gate exits, retain the previous bounded destination.</summary>
    public static EscapeRopeDestination? RecordWarp(EscapeRopeDestination? previous,
        TilePosition sourceWarp, MapId destinationMap, bool route11Rules = false, bool rockTunnelRules = false)
    {
        ArgumentNullException.ThrowIfNull(sourceWarp);
        if (ValidateDestination(previous, route11Rules, rockTunnelRules) is { } error) throw new ArgumentException(error, nameof(previous));
        if (!StableId.IsValid(sourceWarp.MapId.Value) || !StableId.IsValid(destinationMap.Value) ||
            sourceWarp.X < 0 || sourceWarp.Y < 0 || sourceWarp.Elevation is < 0 or > 15 || !Enum.IsDefined(sourceWarp.Facing))
            throw new ArgumentException("Invalid warp source position.", nameof(sourceWarp));

        bool moonEntrance = sourceWarp.MapId.Value == "map.route-4" &&
            destinationMap.Value is "map.mt-moon-1f" or "map.mt-moon-b1f";
        bool forestEntrance = sourceWarp.MapId.Value == "map.route-2" &&
            destinationMap.Value is "map.route-2-forest-south-gate" or "map.route-2-forest-north-gate";
        bool diglettEntrance = (sourceWarp.MapId.Value, destinationMap.Value) is
            ("map.route-11", "map.digletts-cave-south-entrance") or ("map.route-2", "map.digletts-cave-north-entrance");
        bool rockEntrance = sourceWarp.MapId.Value == "map.route-10" && destinationMap.Value == "map.rock-tunnel-1f";
        if (!moonEntrance && !forestEntrance && !diglettEntrance && !rockEntrance) return previous;
        if (diglettEntrance && !(route11Rules || rockTunnelRules) || rockEntrance && !rockTunnelRules)
            return previous;

        bool admitted = (sourceWarp.MapId.Value, destinationMap.Value, sourceWarp.X, sourceWarp.Y, sourceWarp.Facing) switch
        {
            ("map.route-4", "map.mt-moon-1f", 19, 5, Direction.North) => true,
            ("map.route-4", "map.mt-moon-b1f", 32, 5, Direction.North) => true,
            ("map.route-2", "map.route-2-forest-south-gate", 5 or 6, 51, Direction.North) => true,
            ("map.route-2", "map.route-2-forest-north-gate", 5 or 6, 13, Direction.South) => true,
            ("map.route-11", "map.digletts-cave-south-entrance", 6, 7, Direction.North) => true,
            ("map.route-2", "map.digletts-cave-north-entrance", 17, 11, Direction.North) => true,
            ("map.route-10", "map.rock-tunnel-1f", 8, 19 or 57, Direction.North) => true,
            _ => false
        };
        if (!admitted || sourceWarp.Elevation != 3)
            throw new NotSupportedException("Unverified Escape Rope entrance coordinates or direction.");
        // overworld.c UpdateEscapeWarp: x - MAP_OFFSET, y - MAP_OFFSET + (facing != SOUTH).
        return new(sourceWarp.MapId, sourceWarp.X, sourceWarp.Y + (sourceWarp.Facing == Direction.South ? 0 : 1));
    }

    /// <summary>Null remains valid for historical saves, including old in-cave saves. Such saves
    /// cannot use a rope until a real admitted entrance establishes the destination.</summary>
    public static string? ValidateDestination(EscapeRopeDestination? destination, bool route11Rules = false, bool rockTunnelRules = false) => destination switch
    {
        null => null,
        { MapId.Value: "map.route-4", X: 19 or 32, Y: 6 } => null,
        { MapId.Value: "map.route-2", X: 5 or 6, Y: 13 or 52 } => null,
        { MapId.Value: "map.route-11", X: 6, Y: 8 } when route11Rules || rockTunnelRules => null,
        { MapId.Value: "map.route-2", X: 17, Y: 12 } when route11Rules || rockTunnelRules => null,
        { MapId.Value: "map.route-10", X: 8, Y: 20 or 58 } when rockTunnelRules => null,
        _ => "escape_rope_destination_invalid"
    };

    /// <summary>Commit the returned inventory and a pending arrival together when entering the
    /// source used-item message. Acknowledge that message before applying Arrival. The acknowledgement
    /// must not call Use again. Failure preserves the exact inventory and consumes zero.
    /// Application owns field/battle locks, revision guards, movement/RNG, and destination persistence.</summary>
    public static EscapeRopeUseResult Use(EarlyShopCatalog catalog, InventoryState inventory,
        MapId currentMap, EscapeRopeDestination? destination, PotionUseContext context,
        bool route11Rules = false, bool rockTunnelRules = false)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        EscapeRopeUseResult Fail(string error) => new(inventory, null, false, 0, error);
        var error = EarlyShopInventoryRules.Validate(catalog, inventory);
        if (error is not null) return Fail(error);
        if (context != PotionUseContext.Field) return Fail("escape_rope_field_only");
        if (!IsSupportedMap(currentMap, route11Rules, rockTunnelRules)) return Fail("escape_rope_map_not_allowed");
        if (IsRockMap(currentMap) && catalog.Profile is not (RockTunnelItemRules.Profile or LavenderItemRules.Profile or CeladonGymItemRules.Profile or RocketHideoutItemRules.Profile) ||
            IsDiglettMap(currentMap) && catalog.Profile is not (Route11ItemRules.Profile or RockTunnelItemRules.Profile or LavenderItemRules.Profile or CeladonGymItemRules.Profile or RocketHideoutItemRules.Profile))
            return Fail("escape_rope_map_not_allowed");
        if (destination is null) return Fail("escape_rope_destination_missing");
        if (ValidateDestination(destination, route11Rules, rockTunnelRules) is { } invalid) return Fail(invalid);
        bool matches = currentMap.Value == "map.viridian-forest" ?
            destination is { MapId.Value: "map.route-2", X: 5 or 6, Y: 13 or 52 } :
            IsDiglettMap(currentMap) ? destination is { MapId.Value: "map.route-11", X: 6, Y: 8 } or { MapId.Value: "map.route-2", X: 17, Y: 12 } :
            IsRockMap(currentMap) ? destination is { MapId.Value: "map.route-10", X: 8, Y: 20 or 58 } :
            destination is { MapId.Value: "map.route-4", X: 19 or 32, Y: 6 };
        if (!matches) return Fail("escape_rope_destination_mismatch");
        int quantity = OpeningInventoryRules.Quantity(inventory.BagItems, Item);
        if (quantity == 0) return Fail("escape_rope_not_in_bag");
        var definition = catalog.Items.Single(i => i.Id == Item);
        var arrival = new TilePosition(destination.MapId, destination.X, destination.Y, 3,
            destination.MapId.Value == "map.route-2" && destination.Y == 13 ? Direction.North : Direction.South);
        return new(EarlyShopInventoryRules.SetQuantity(inventory, definition, quantity - 1), arrival, true, 1, null);
    }
}
