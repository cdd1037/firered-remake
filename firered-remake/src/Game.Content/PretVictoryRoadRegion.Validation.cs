using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;
public sealed partial class PretVictoryRoadRegion
{
    private static SilphSourcePackage VictoryRoadSource(string root) => new(root,"victory-road-region",        "content/sources/victory-road-region.lock.json");

    private static void ValidateContracts(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        var contract = source.Document("contracts.json");
        Need(!contract.GetProperty("runtimeIntegrated").GetBoolean() && !source.Document("declarations.json").GetProperty("ownedCatalogModified").GetBoolean(),
            "Source evidence cannot admit runtime or ownership changes.");
        var sizes = new[] { ("map.route-23", 24, 160),("map.victory-road-1f", 48, 22),("map.victory-road-2f", 51, 22),("map.victory-road-3f", 45, 22),("map.indigo-plateau-exterior", 24, 20) };
        foreach (var (id, width, height) in sizes)
        {
            var m = maps[new(id)];
            Need(m.Width == width && m.Height == height && m.BorderWidth == 2 && m.BorderHeight == 2 && !m.Events.GetProperty("requires_flash").GetBoolean(),
                "Original League approach dimensions/Flash requirement differ.");
        }
        var gates = contract.GetProperty("badgeGates").EnumerateArray().ToArray();
        Need(gates.Length == 8 && gates.Select(g => g.GetProperty("badge").GetString()).SequenceEqual(new[] { "Boulder","Cascade","Thunder","Rainbow","Soul","Marsh","Volcano","Earth" }), "Original badge gate order differs.");
        for (int i = 0; i < gates.Length; i++)
        {
            var g = gates[i];
            Need(g.GetProperty("order").GetInt32() == i + 1 && g.GetProperty("flag").GetString() == $"FLAG_BADGE{i + 1:00}_GET" &&
                g.GetProperty("triggers").EnumerateArray().All(t => t.GetProperty("var").GetString() == "VAR_MAP_SCENE_ROUTE23" && t.GetProperty("var_value").GetString() == i.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                "Original sequential badge gate scene differs.");
            var path = i == 0 ? "data/maps/Route22_NorthEntrance/map.json" : "data/maps/Route23/map.json";
            var events = SilphSourcePackage.Json(source.Raw[path]);
            Need(JsonElement.DeepEquals(events.GetProperty("object_events")[g.GetProperty("localId").GetInt32() - 1], g.GetProperty("event")) &&
                g.GetProperty("triggers").EnumerateArray().All(t => events.GetProperty("coord_events").EnumerateArray().Any(e => JsonElement.DeepEquals(e, t))), "Gate actor or trigger differs.");
        }
        var expectedSwitches = new[] { ("map.victory-road-1f", 20, 16),("map.victory-road-2f", 2, 19),("map.victory-road-2f", 14, 19),("map.victory-road-3f", 7, 7) };
        var switches = contract.GetProperty("switches").EnumerateArray().ToArray();
        Need(switches.Length == 4, "Victory Road switch count differs.");
        for (int i = 0; i < switches.Length; i++)
        {
            var s = switches[i]; var e = s.GetProperty("event"); var (id, x, y) = expectedSwitches[i]; var m = maps[new(id)];
            Need(MapIdFromSourceSymbol(s.GetProperty("map").GetString()!).Value == id && e.GetProperty("x").GetInt32() == x && e.GetProperty("y").GetInt32() == y &&
                e.GetProperty("var_value").GetString() == "99" && s.GetProperty("pressedValue").GetInt32() == 100 && m.SourceBehaviorAt(x, y) == constants["MB_STRENGTH_BUTTON"] &&
                s.GetProperty("dispatch").GetString() == "BoulderPushCompletionDirectCoordScript" && !s.GetProperty("runtimeAdmitted").GetBoolean(), "Original boulder switch dispatch differs.");
            Need(m.Events.GetProperty("coord_events").EnumerateArray().Any(a => JsonElement.DeepEquals(a, e)), "Switch coordinate source differs.");
        }
        Need(contract.GetProperty("boulders").GetArrayLength() == 10 && contract.GetProperty("itemPickups").GetArrayLength() == 8 && contract.GetProperty("hiddenItems").GetArrayLength() == 10 &&
            contract.GetProperty("trainerOccurrences").GetArrayLength() == 13 && source.Document("declarations.json").GetProperty("trainers").GetArrayLength() == 12,
            "Original actor/item/trainer scope differs.");
        var fall = contract.GetProperty("fallingBoulder");
        Need(fall.GetProperty("trainerTypeIsRevealFlag").GetBoolean() && !fall.GetProperty("runtimeCertified").GetBoolean() &&
            fall.GetProperty("source").GetProperty("event").GetProperty("trainer_type").GetString() == "FLAG_HIDE_VICTORY_ROAD_2F_BOULDER" &&
            maps[new("map.victory-road-3f")].SourceBehaviorAt(34, 18) == constants["MB_FALL_WARP"] &&
            fall.GetProperty("destination").GetProperty("localId").GetInt32() == 12, "Original falling boulder link differs.");
        Need(contract.GetProperty("persistence").GetProperty("runtimeCertified").GetBoolean() == false &&
            contract.GetProperty("externalBoundaries")[0].GetProperty("destination").GetString() == "MAP_INDIGO_PLATEAU_POKEMON_CENTER_1F" &&
            !contract.GetProperty("externalBoundaries")[0].GetProperty("runtimeAdmitted").GetBoolean(), "Unadmitted state or League boundary changed.");
        foreach (var block in source.Document("scripts.json").GetProperty("blocks").EnumerateArray())
        {
            var reference = block.GetProperty("sourceRef"); var path = reference.GetProperty("path").GetString()!;
            var original = Encoding.UTF8.GetString(source.Raw[path]).Split('\n');
            Need(original[reference.GetProperty("line").GetInt32() - 1].TrimEnd(':', '\r') == block.GetProperty("label").GetString(), "Script label line differs.");
            foreach (var line in block.GetProperty("lines").EnumerateArray()) Need(original[line.GetProperty("line").GetInt32() - 1].TrimEnd('\r') == line.GetProperty("text").GetString(), "Script transcript differs.");
        }
        foreach (var row in source.Document("catalog.json").GetProperty("mapSymbols").EnumerateObject())
            Need(MapIdFromSourceSymbol(row.Name).Value == row.Value.GetString(), "League approach source map symbol differs.");
    }
}
