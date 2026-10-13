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
        return new(r.GetProperty("path").GetString()!, r.GetProperty("symbol").GetString()!, r.GetProperty("sha256").GetString()!);
    }
    private static EventIslandFieldData LoadField(SilphSourcePackage source, ImmutableDictionary<string, int> constants)
    {
        var contract = source.Document("contracts.json");
        var headers = contract.GetProperty("mapHeaders").EnumerateArray().Select(r => new EventIslandMapHeader(
            MapIdFromSourceSymbol(r.GetProperty("map").GetString()!), r.GetProperty("music").GetString()!, r.GetProperty("weather").GetString()!,
            r.GetProperty("map_type").GetString()!, r.GetProperty("allow_cycling").GetBoolean(), r.GetProperty("allow_escaping").GetBoolean(),
            r.GetProperty("allow_running").GetBoolean(), r.GetProperty("requires_flash").GetBoolean(), r.GetProperty("show_map_name").GetBoolean(),
            r.GetProperty("floor_number").GetInt32(), r.GetProperty("battle_scene").GetString()!, false, Reference(r))).ToImmutableArray();
        var pickups = contract.GetProperty("hiddenItems").EnumerateArray().Select(r =>
        {
            var e = r.GetProperty("event"); string item = r.GetProperty("item").GetString()!, flag = e.GetProperty("flag").GetString()!;
            return new EventIslandHiddenPickup(MapIdFromSourceSymbol(r.GetProperty("map").GetString()!), e.GetProperty("x").GetInt32(),
                e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(), new("pret:" + item), item, constants[item],
                r.GetProperty("quantity").GetInt32(), flag, constants[flag], e.GetProperty("underfoot").GetBoolean(), r.GetProperty("requiresItemfinder").GetBoolean(),
                contract.GetProperty("sacredAshBoundary").GetProperty("runtimePickupAdmitted").GetBoolean(), Reference(r));
        }).ToImmutableArray();
        var coordinates = contract.GetProperty("coordinateTriggers").EnumerateArray().Select(r =>
        {
            var e = r.GetProperty("event"); string variable = e.GetProperty("var").GetString()!;
            return new EventIslandCoordinateTrigger(MapIdFromSourceSymbol(r.GetProperty("map").GetString()!), e.GetProperty("x").GetInt32(),
                e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(), variable, constants[variable],
                int.Parse(e.GetProperty("var_value").GetString()!, CultureInfo.InvariantCulture), e.GetProperty("script").GetString()!, Reference(r));
        }).ToImmutableArray();
        var landings = contract.GetProperty("ferryLandings").EnumerateArray().Select(r => new EventIslandFerryLanding(
            MapIdFromSourceSymbol(r.GetProperty("map").GetString()!), r.GetProperty("x").GetInt32(), r.GetProperty("y").GetInt32(), Direction.South, Reference(r))).ToImmutableArray();
        var graphics = contract.GetProperty("dynamicGraphics").EnumerateArray().Select(r => new EventIslandDynamicGraphics(
            MapIdFromSourceSymbol(r.GetProperty("map").GetString()!), r.GetProperty("localId").GetInt32(), r.GetProperty("sourceGraphics").GetString()!,
            r.GetProperty("variable").GetString()!, r.GetProperty("graphicsId").GetString()!, r.GetProperty("script").GetString()!)).ToImmutableArray();
        return new(headers, pickups, coordinates, landings, graphics, contract.GetProperty("nativeCamera").GetProperty("sourceId").GetInt32());
    }

    private static void ValidateContracts(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants)
    {
        var contract = source.Document("contracts.json");
        Need(maps.Count == 24 && maps.Values.Sum(m => m.Events.GetProperty("object_events").GetArrayLength()) == 8 &&
            maps.Values.Sum(m => m.Events.GetProperty("coord_events").GetArrayLength()) == 1 && contract.GetProperty("hiddenItems").GetArrayLength() == 1,
            "Event island map/object/trigger scope differs.");
        var topology = contract.GetProperty("mapTopology");
        Need(topology.GetProperty("warps").GetArrayLength() == 44 && topology.GetProperty("connections").GetArrayLength() == 0 &&
            contract.GetProperty("externalBoundaries").GetProperty("warps").GetArrayLength() == 0 &&
            contract.GetProperty("externalBoundaries").GetProperty("connections").GetArrayLength() == 0, "Event island boundaries differ.");
        foreach (var row in topology.GetProperty("warps").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(row.GetProperty("sourceMap").GetString()!)]; int index = row.GetProperty("sourceIndex").GetInt32();
            var original = map.Events.GetProperty("warp_events")[index];
            var target = maps[MapIdFromSourceSymbol(original.GetProperty("dest_map").GetString()!)];
            var back = target.Events.GetProperty("warp_events")[int.Parse(original.GetProperty("dest_warp_id").GetString()!, CultureInfo.InvariantCulture)];
            Need(JsonElement.DeepEquals(original, row.GetProperty("source")) && JsonElement.DeepEquals(back, row.GetProperty("destination")) &&
                back.GetProperty("dest_map").GetString() == map.Events.GetProperty("id").GetString() &&
                int.Parse(back.GetProperty("dest_warp_id").GetString()!, CultureInfo.InvariantCulture) == index &&
                row.GetProperty("destinationInSlice").GetBoolean() && row.GetProperty("reciprocalIndex").GetBoolean(), "Original reciprocal warp differs.");
        }
        Need(contract.GetProperty("mapHeaders").GetArrayLength() == 24, "Missing event-island headers.");
        foreach (var row in contract.GetProperty("mapHeaders").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(row.GetProperty("map").GetString()!)];
            foreach (string key in new[] { "music", "weather", "map_type", "allow_cycling", "allow_escaping", "allow_running", "requires_flash", "show_map_name", "floor_number", "battle_scene" })
                Need(JsonElement.DeepEquals(row.GetProperty(key), map.Events.GetProperty(key)), "Original map header differs: " + key);
        }
        var trigger = contract.GetProperty("coordinateTriggers").EnumerateArray().Single();
        var summit = maps[new("map.navel-rock-summit")];
        Need(trigger.GetProperty("map").GetString() == "MAP_NAVEL_ROCK_SUMMIT" && JsonElement.DeepEquals(trigger.GetProperty("event"), summit.Events.GetProperty("coord_events")[0]) &&
            summit.Events.GetProperty("object_events")[0].GetProperty("script").GetString() == "0x0", "Ho-Oh must remain a source coordinate encounter.");
        var hidden = contract.GetProperty("hiddenItems").EnumerateArray().Single();
        Need(JsonElement.DeepEquals(hidden.GetProperty("event"), summit.Events.GetProperty("bg_events")[0]) && hidden.GetProperty("requiresItemfinder").GetBoolean() &&
            hidden.GetProperty("event").GetProperty("underfoot").GetBoolean() && hidden.GetProperty("item").GetString() == "ITEM_SACRED_ASH" &&
            !contract.GetProperty("sacredAshBoundary").GetProperty("runtimePickupAdmitted").GetBoolean(), "Sacred Ash prerequisite boundary differs.");
        Need(Encoding.UTF8.GetString(source.Raw["src/field_control_avatar.c"]).Contains(
            "if (GetHiddenItemAttr(bgEvent->bgUnion.hiddenItem, HIDDEN_ITEM_UNDERFOOT) == TRUE)\n            return NULL;", StringComparison.Ordinal) &&
            Encoding.UTF8.GetString(source.Raw["src/itemfinder.c"]).Contains("GetHiddenItemAttr(events->bgEvents[i].bgUnion.hiddenItem, HIDDEN_ITEM_UNDERFOOT) == TRUE", StringComparison.Ordinal),
            "Underfoot Itemfinder source rule differs.");
        Need(contract.GetProperty("randomEncounterHeaders").GetArrayLength() == 0, "Unexpected event-island random encounters.");
        var symbols = maps.Values.Select(m => m.Events.GetProperty("id").GetString()).ToHashSet(StringComparer.Ordinal);
        var encounters = SilphSourcePackage.Json(source.Raw["src/data/wild_encounters.json"]);
        Need(!encounters.GetProperty("wild_encounter_groups").EnumerateArray().Where(g => g.TryGetProperty("encounters", out _))
            .SelectMany(g => g.GetProperty("encounters").EnumerateArray()).Any(e => e.TryGetProperty("map", out var symbol) && symbols.Contains(symbol.GetString())),
            "Source contains an unaccounted random encounter header.");
        var graphic = contract.GetProperty("dynamicGraphics").EnumerateArray().Single();
        Need(graphic.GetProperty("sourceGraphics").GetString() == "OBJ_EVENT_GFX_VAR_0" && graphic.GetProperty("graphicsId").GetString() == "OBJ_EVENT_GFX_DEOXYS_N" &&
            maps[new("map.birth-island-exterior")].Events.GetProperty("object_events")[1].GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_VAR_0" &&
            Encoding.UTF8.GetString(source.Raw["data/maps/BirthIsland_Exterior/scripts.inc"]).Contains("setvar VAR_OBJ_GFX_ID_0, OBJ_EVENT_GFX_DEOXYS_N", StringComparison.Ordinal),
            "Original dynamic Deoxys overworld graphics differs.");
        Need(constants["LOCALID_CAMERA"] == 127 && contract.GetProperty("nativeCamera").GetProperty("sourceId").GetInt32() == 127 &&
            !contract.GetProperty("nativeCamera").GetProperty("initialMapObject").GetBoolean(), "Native camera identity differs.");
        foreach (var row in source.Document("catalog.json").GetProperty("mapSymbols").EnumerateObject())
            Need(MapIdFromSourceSymbol(row.Name).Value == row.Value.GetString(), "Event island map resolver differs.");
    }
}
