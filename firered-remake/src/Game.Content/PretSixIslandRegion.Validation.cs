using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;
public sealed partial class PretSixIslandRegion
{
    private static void ValidateContracts(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants)
    {
        var contract = source.Document("contracts.json");
        Need(contract.GetProperty("itemPickups").GetArrayLength() == 5 && contract.GetProperty("hiddenItems").GetArrayLength() == 4 &&
            source.Document("npc-source.json").GetProperty("graphics").GetArrayLength() == 21 &&
            source.Document("declarations.json").GetProperty("trainers").GetArrayLength() == 11 &&
            maps.Values.Sum(m => m.Events.GetProperty("object_events").GetArrayLength()) == 39, "Six Island complete field inventory differs.");
        foreach (var item in contract.GetProperty("itemPickups").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(item.GetProperty("map").GetString()!)];
            var original = map.Events.GetProperty("object_events")[item.GetProperty("localId").GetInt32() - 1];
            Need(JsonElement.DeepEquals(item.GetProperty("event"), original) && original.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_ITEM_BALL" &&
                item.GetProperty("script").GetProperty("label").GetString() == original.GetProperty("script").GetString() && constants.ContainsKey(original.GetProperty("flag").GetString()!), "Visible pickup differs.");
        }
        foreach (var item in contract.GetProperty("hiddenItems").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(item.GetProperty("map").GetString()!)]; var original = item.GetProperty("event");
            Need(map.Events.GetProperty("bg_events").EnumerateArray().Count(e => JsonElement.DeepEquals(e, original)) == 1 &&
                constants.ContainsKey(original.GetProperty("item").GetString()!) && constants.ContainsKey(original.GetProperty("flag").GetString()!), "Hidden pickup differs.");
        }
        var rawMaps = source.Raw.Where(p => p.Key.StartsWith("data/maps/", StringComparison.Ordinal) && p.Key.EndsWith("/map.json", StringComparison.Ordinal))
            .Select(p => SilphSourcePackage.Json(p.Value)).ToDictionary(e => e.GetProperty("id").GetString()!, StringComparer.Ordinal);
        var topology = contract.GetProperty("mapTopology");
        Need(topology.GetProperty("warps").GetArrayLength() == 37 && topology.GetProperty("connections").GetArrayLength() == 5, "Complete map topology differs.");
        foreach (var row in topology.GetProperty("warps").EnumerateArray())
        {
            var symbol = row.GetProperty("sourceMap").GetString()!; var index = row.GetProperty("sourceIndex").GetInt32();
            var original = rawMaps[symbol].GetProperty("warp_events")[index]; var targetSymbol = original.GetProperty("dest_map").GetString()!;
            var target = rawMaps[targetSymbol].GetProperty("warp_events")[int.Parse(original.GetProperty("dest_warp_id").GetString()!, CultureInfo.InvariantCulture)];
            var reciprocalMap = target.GetProperty("dest_map").GetString() == symbol;
            Need(JsonElement.DeepEquals(original, row.GetProperty("source")) && JsonElement.DeepEquals(target, row.GetProperty("destination")) &&
                row.GetProperty("destinationInSlice").GetBoolean() == maps.ContainsKey(MapIdFromSourceSymbol(targetSymbol)) &&
                row.GetProperty("reciprocalMap").GetBoolean() == reciprocalMap && row.GetProperty("reciprocalIndex").GetBoolean() ==
                    (reciprocalMap && int.Parse(target.GetProperty("dest_warp_id").GetString()!, CultureInfo.InvariantCulture) == index), "Original one-way or reciprocal warp differs.");
        }
        foreach (var row in topology.GetProperty("connections").EnumerateArray())
        {
            var original = row.GetProperty("source"); var symbol = row.GetProperty("sourceMap").GetString()!;
            Need(rawMaps[symbol].GetProperty("connections").EnumerateArray().Any(e => JsonElement.DeepEquals(e, original)) &&
                row.GetProperty("destinationInSlice").GetBoolean() == maps.ContainsKey(MapIdFromSourceSymbol(original.GetProperty("map").GetString()!)), "Connection differs.");
        }
        var external = contract.GetProperty("externalBoundaries");
        Need(external.GetProperty("warps").EnumerateArray().Select(w => w.GetProperty("source").GetProperty("dest_map").GetString()).ToHashSet().SetEquals(
            ["MAP_SIX_ISLAND_HOUSE", "MAP_SIX_ISLAND_WATER_PATH_HOUSE1", "MAP_SIX_ISLAND_WATER_PATH_HOUSE2", "MAP_SIX_ISLAND_POKEMON_CENTER_2F"]) &&
            external.GetProperty("warps").GetArrayLength() == 4 && external.GetProperty("connections").GetArrayLength() == 1 &&
            external.GetProperty("connections")[0].GetProperty("source").GetProperty("map").GetString() == "MAP_SIX_ISLAND_GREEN_PATH", "Exact external boundaries differ.");
        Need(contract.GetProperty("excludedEncounterGates").GetArrayLength() == 0 && maps.Keys.All(k => k.Value.StartsWith("map.six-island", StringComparison.Ordinal)) &&
            !maps.ContainsKey(new("map.six-island-green-path")), "Unadmitted geography or encounter gating differs.");
        ValidateDottedHole(source, contract, maps, constants);
        foreach (var row in source.Document("catalog.json").GetProperty("mapSymbols").EnumerateObject())
            Need(MapIdFromSourceSymbol(row.Name).Value == row.Value.GetString(), "Six Island map resolver differs.");
    }
    private static void ValidateDottedHole(SilphSourcePackage source, JsonElement contract, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants)
    {
        void Cell(JsonElement row, PretWorldMap map)
        {
            var cell = map.GetSourceCell(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32());
            Need(cell is not null && cell.RawBlock == row.GetProperty("rawBlock").GetInt32() && cell.RawAttributes == row.GetProperty("rawAttributes").GetUInt32() &&
                cell.BehaviorId == row.GetProperty("behaviorId").GetInt32(), "Field anchor differs from source cell.");
        }
        var falls = contract.GetProperty("fallWarps").EnumerateArray().ToArray();
        Need(falls.Length == 17 && maps.Values.Sum(m => m.Cells.Count(c => c.BehaviorId == 0x66)) == 17, "Seventeen complete fall-warp cells required.");
        foreach (var row in falls)
        {
            var map = maps[MapIdFromSourceSymbol(row.GetProperty("sourceMap").GetString()!)]; var original = map.Events.GetProperty("warp_events")[row.GetProperty("sourceIndex").GetInt32()];
            var target = maps[MapIdFromSourceSymbol(row.GetProperty("destinationMap").GetString()!)];
            Need(JsonElement.DeepEquals(original, row.GetProperty("source")) && row.GetProperty("sourceCell").GetProperty("behaviorId").GetInt32() == 0x66 &&
                JsonElement.DeepEquals(target.Events.GetProperty("warp_events")[row.GetProperty("destinationIndex").GetInt32()], row.GetProperty("destination")), "Fall-warp source/destination differs.");
            Cell(row.GetProperty("sourceCell"), map); Cell(row.GetProperty("destinationCell"), target);
        }
        var correct = contract.GetProperty("correctFallRoute").EnumerateArray().ToArray();
        Need(correct.Length == 5 && correct.Select(r => r.GetProperty("sourceIndex").GetInt32()).SequenceEqual(new[] { 1,1,4,2,3 }) &&
            correct.Select(r => r.GetProperty("destinationMap").GetString()).SequenceEqual(new[] { "MAP_SIX_ISLAND_DOTTED_HOLE_B1F","MAP_SIX_ISLAND_DOTTED_HOLE_B2F","MAP_SIX_ISLAND_DOTTED_HOLE_B3F","MAP_SIX_ISLAND_DOTTED_HOLE_B4F","MAP_SIX_ISLAND_DOTTED_HOLE_SAPPHIRE_ROOM" }), "Original Up/Left/Right/Down solution differs.");
        var wrong = contract.GetProperty("wrongFallWarps").EnumerateArray().ToArray();
        Need(wrong.Length == 12 && wrong.All(r => r.GetProperty("destinationMap").GetString() == "MAP_SIX_ISLAND_DOTTED_HOLE_1F" &&
            r.GetProperty("destinationIndex").GetInt32() == 3 && r.GetProperty("destination").GetProperty("x").GetInt32() == 6 && r.GetProperty("destination").GetProperty("y").GetInt32() == 7), "Incorrect falls must retain 1F warp3 landing.");
        var door = contract.GetProperty("dottedHoleDoor"); Cell(door.GetProperty("sourceCell"), maps[new("map.six-island-ruin-valley")]);
        Need(door.GetProperty("playerX").GetInt32() == 24 && door.GetProperty("playerY").GetInt32() == 25 && door.GetProperty("facing").GetString() == "DIR_NORTH" &&
            door.GetProperty("x").GetInt32() == 24 && door.GetProperty("y").GetInt32() == 24 && door.GetProperty("bufferedX").GetInt32() == 31 && door.GetProperty("bufferedY").GetInt32() == 31 &&
            door.GetProperty("openCollision").GetInt32() == 0 && door.GetProperty("flag").GetString() == "FLAG_USED_CUT_ON_RUIN_VALLEY_BRAILLE", "Special Cut door coordinates/flag differ.");
        foreach (var patch in door.GetProperty("metatiles").EnumerateArray())
        {
            var id = patch.GetProperty("metatileId").GetInt32(); var attr = BinaryPrimitives.ReadUInt32LittleEndian(source.Raw["data/tilesets/secondary/sevii_islands_67/metatile_attributes.bin"].AsSpan((id - 640) * 4));
            Need(id is 0x357 or 0x358 && constants[patch.GetProperty("symbol").GetString()!] == id && patch.GetProperty("rawAttributes").GetUInt32() == attr, "Door complete tileset source differs.");
        }
        var theft = contract.GetProperty("sapphireTheft"); var blocks = source.Document("scripts.json").GetProperty("blocks").EnumerateArray().ToDictionary(b => b.GetProperty("label").GetString()!);
        void Block(JsonElement b) => Need(JsonElement.DeepEquals(b, blocks[b.GetProperty("label").GetString()!]), "Field source block differs from authenticated inventory.");
        Block(contract.GetProperty("fallWarpScript")); Block(theft.GetProperty("script")); Block(door.GetProperty("onLoad")); Block(door.GetProperty("openScript"));
        Need(!theft.GetProperty("grantItem").GetBoolean() && theft.GetProperty("endpointFlag").GetString() == "FLAG_LEARNED_YES_NAH_CHANSEY" &&
            constants["FLAG_LEARNED_YES_NAH_CHANSEY"] == 0x2D8 && theft.GetProperty("branches").GetArrayLength() == 4, "Sapphire theft endpoint differs.");
        foreach (var branch in theft.GetProperty("branches").EnumerateArray())
        {
            Need(branch.GetProperty("calls").GetArrayLength() == 5, "Five original facing-dependent stages required.");
            foreach (var b in branch.GetProperty("calls").EnumerateArray()) Block(b);
        }
        Need(JsonElement.DeepEquals(theft.GetProperty("sourceActors"), maps[new("map.six-island-dotted-hole-sapphire-room")].Events.GetProperty("object_events")), "Sapphire and thief actors differ.");
        Need(contract.GetProperty("directionalBraille").GetArrayLength() == 4 && contract.GetProperty("sapphireBraille").GetProperty("parts").GetArrayLength() == 16, "Complete directional/plaque Braille required.");
        foreach (var row in contract.GetProperty("directionalBraille").EnumerateArray())
        {
            Block(row.GetProperty("script")); Block(row.GetProperty("text"));
            Need(row.GetProperty("script").GetProperty("sourceRef").GetProperty("path").GetString() == "data/maps/SixIsland_DottedHole_1F/scripts.inc", "Directional Braille must retain cross-map source.");
        }
        Block(contract.GetProperty("sapphireBraille").GetProperty("script")); foreach (var b in contract.GetProperty("sapphireBraille").GetProperty("parts").EnumerateArray()) Block(b);
        Need(contract.GetProperty("strengthBoulders").GetArrayLength() == 8 && constants["HEAL_LOCATION_SIX_ISLAND"] == 20, "Strength or heal source differs.");
        foreach (var row in contract.GetProperty("mapHeaders").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(row.GetProperty("map").GetString()!)];
            Need(row.GetProperty("allowCycling").GetBoolean() == map.Events.GetProperty("allow_cycling").GetBoolean() &&
                row.GetProperty("allowEscaping").GetBoolean() == map.Events.GetProperty("allow_escaping").GetBoolean() && row.GetProperty("allowRunning").GetBoolean() == map.Events.GetProperty("allow_running").GetBoolean(), "Source movement header differs.");
        }
        Need(maps.Values.Where(m => m.Id.Value.Contains("dotted-hole", StringComparison.Ordinal)).All(m => m.Events.GetProperty("allow_cycling").GetBoolean() && m.Events.GetProperty("allow_escaping").GetBoolean()), "Original cave cycling/Escape Rope removed.");
    }
}
