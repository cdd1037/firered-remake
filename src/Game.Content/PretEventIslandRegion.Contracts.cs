using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretEventIslandRegion
{
    private static EventIslandFieldSource Reference(JsonElement value)
    {
        var r = value.GetProperty("sourceRef");
        return new(r.GetProperty("path").GetString()!, r.GetProperty("symbol").GetString()!);
    }
    private static EventIslandFieldData LoadField(PretCurrentWorld world, ImmutableDictionary<MapId, PretWorldMap> maps, SilphSourcePackage source, ImmutableDictionary<string, int> constants)
    {
        var contract = source.Document("contracts.json");
        MapId Local(string symbol)
        {
            var id = world.Resolve(symbol);
            Need(maps.ContainsKey(id), "Event Island field map is not mounted.");
            return id;
        }
        foreach (var row in contract.GetProperty("mapHeaders").EnumerateArray())
        {
            var map = maps[Local(row.GetProperty("map").GetString()!)];
            foreach (string property in new[] { "music", "weather", "map_type", "allow_cycling", "allow_escaping", "allow_running", "requires_flash", "show_map_name", "floor_number", "battle_scene" })
                Need(JsonElement.DeepEquals(map.Events.GetProperty(property), row.GetProperty(property)), "Event Island map header differs.");
        }
        foreach (string kind in new[] { "hiddenItems", "coordinateTriggers" })
        foreach (var row in contract.GetProperty(kind).EnumerateArray())
        {
            var map = maps[Local(row.GetProperty("map").GetString()!)];
            var e = row.GetProperty("event");
            Need(map.GetSourceCell(e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32()) is not null &&
                map.Events.GetProperty(kind == "hiddenItems" ? "bg_events" : "coord_events").EnumerateArray().Any(a => JsonElement.DeepEquals(a, e)),
                "Event Island event endpoint differs.");
        }
        foreach (var row in contract.GetProperty("ferryLandings").EnumerateArray())
            Need(maps[Local(row.GetProperty("map").GetString()!)].GetSourceCell(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32()) is not null,
                "Event Island ferry endpoint differs.");
        foreach (var row in contract.GetProperty("dynamicGraphics").EnumerateArray())
        {
            var actors = maps[Local(row.GetProperty("map").GetString()!)].Events.GetProperty("object_events");
            int local = row.GetProperty("localId").GetInt32();
            Need(local > 0 && local <= actors.GetArrayLength() && actors[local - 1].GetProperty("graphics_id").GetString() == row.GetProperty("sourceGraphics").GetString(),
                "Event Island graphics actor endpoint differs.");
        }
        var headers = contract.GetProperty("mapHeaders").EnumerateArray().Select(r => new EventIslandMapHeader(
            Local(r.GetProperty("map").GetString()!), r.GetProperty("music").GetString()!, r.GetProperty("weather").GetString()!,
            r.GetProperty("map_type").GetString()!, r.GetProperty("allow_cycling").GetBoolean(), r.GetProperty("allow_escaping").GetBoolean(),
            r.GetProperty("allow_running").GetBoolean(), r.GetProperty("requires_flash").GetBoolean(), r.GetProperty("show_map_name").GetBoolean(),
            r.GetProperty("floor_number").GetInt32(), r.GetProperty("battle_scene").GetString()!, false, Reference(r))).ToImmutableArray();
        var pickups = contract.GetProperty("hiddenItems").EnumerateArray().Select(r =>
        {
            var e = r.GetProperty("event"); string item = r.GetProperty("item").GetString()!,flag = e.GetProperty("flag").GetString()!;
            return new EventIslandHiddenPickup(Local(r.GetProperty("map").GetString()!), e.GetProperty("x").GetInt32(),
                e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(), new("pret:" + item), item, constants[item],
                r.GetProperty("quantity").GetInt32(), flag, constants[flag], e.GetProperty("underfoot").GetBoolean(), r.GetProperty("requiresItemfinder").GetBoolean(),
                contract.GetProperty("sacredAshBoundary").GetProperty("runtimePickupAdmitted").GetBoolean(), Reference(r));
        }).ToImmutableArray();
        var coordinates = contract.GetProperty("coordinateTriggers").EnumerateArray().Select(r =>
        {
            var e = r.GetProperty("event"); string variable = e.GetProperty("var").GetString()!;
            return new EventIslandCoordinateTrigger(Local(r.GetProperty("map").GetString()!), e.GetProperty("x").GetInt32(),
                e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(), variable, constants[variable],
                int.Parse(e.GetProperty("var_value").GetString()!, CultureInfo.InvariantCulture), e.GetProperty("script").GetString()!, Reference(r));
        }).ToImmutableArray();
        var landings = contract.GetProperty("ferryLandings").EnumerateArray().Select(r => new EventIslandFerryLanding(
            Local(r.GetProperty("map").GetString()!), r.GetProperty("x").GetInt32(), r.GetProperty("y").GetInt32(), Direction.South, Reference(r))).ToImmutableArray();
        var graphics = contract.GetProperty("dynamicGraphics").EnumerateArray().Select(r => new EventIslandDynamicGraphics(
            Local(r.GetProperty("map").GetString()!), r.GetProperty("localId").GetInt32(), r.GetProperty("sourceGraphics").GetString()!,
            r.GetProperty("variable").GetString()!, r.GetProperty("graphicsId").GetString()!, r.GetProperty("script").GetString()!)).ToImmutableArray();
        return new(headers, pickups, coordinates, landings, graphics, contract.GetProperty("nativeCamera").GetProperty("sourceId").GetInt32());
    }

}
