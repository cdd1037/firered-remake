using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;
public sealed partial class PretFourIslandRegion
{
    private static void ValidateContracts(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants)
    {
        var contract = source.Document("contracts.json");
        Need(contract.GetProperty("itemPickups").GetArrayLength() == 6 && contract.GetProperty("hiddenItems").GetArrayLength() == 2 &&
            source.Document("npc-source.json").GetProperty("graphics").GetArrayLength() == 22 &&
            source.Document("declarations.json").GetProperty("trainers").GetArrayLength() == 1, "Four Island source field scope differs.");
        foreach (var item in contract.GetProperty("itemPickups").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(item.GetProperty("map").GetString()!)];
            var localId = item.GetProperty("localId").GetInt32();
            var original = map.Events.GetProperty("object_events")[localId - 1];
            Need(JsonElement.DeepEquals(item.GetProperty("event"), original) && original.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_ITEM_BALL" &&
                item.GetProperty("script").GetProperty("label").GetString() == original.GetProperty("script").GetString() &&
                constants.ContainsKey(original.GetProperty("flag").GetString()!), "Four Island visible pickup differs from source event.");
        }
        foreach (var item in contract.GetProperty("hiddenItems").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(item.GetProperty("map").GetString()!)];
            var original = item.GetProperty("event");
            Need(map.Events.GetProperty("bg_events").EnumerateArray().Count(e => JsonElement.DeepEquals(e, original)) == 1 &&
                constants.ContainsKey(original.GetProperty("item").GetString()!) && constants.ContainsKey(original.GetProperty("flag").GetString()!),
                "Four Island hidden pickup differs from source event.");
        }
        var rawMaps = source.Raw.Where(p => p.Key.StartsWith("data/maps/", StringComparison.Ordinal) && p.Key.EndsWith("/map.json", StringComparison.Ordinal))
            .Select(p => SilphSourcePackage.Json(p.Value)).ToDictionary(e => e.GetProperty("id").GetString()!, StringComparer.Ordinal);
        foreach (var row in contract.GetProperty("mapTopology").GetProperty("warps").EnumerateArray())
        {
            var original = rawMaps[row.GetProperty("sourceMap").GetString()!].GetProperty("warp_events")[row.GetProperty("sourceIndex").GetInt32()];
            var targetSymbol = original.GetProperty("dest_map").GetString()!;
            var target = rawMaps[targetSymbol].GetProperty("warp_events")[int.Parse(original.GetProperty("dest_warp_id").GetString()!, System.Globalization.CultureInfo.InvariantCulture)];
            Need(JsonElement.DeepEquals(original, row.GetProperty("source")) && JsonElement.DeepEquals(target, row.GetProperty("destination")) &&
                row.GetProperty("destinationInSlice").GetBoolean() == maps.ContainsKey(MapIdFromSourceSymbol(targetSymbol)), "Four Island warp boundary differs.");
        }
        foreach (var row in contract.GetProperty("mapTopology").GetProperty("connections").EnumerateArray())
        {
            var original = row.GetProperty("source"); var symbol = row.GetProperty("sourceMap").GetString()!;
            Need(rawMaps[symbol].GetProperty("connections").EnumerateArray().Any(e => JsonElement.DeepEquals(e, original)) &&
                row.GetProperty("destinationInSlice").GetBoolean() == maps.ContainsKey(MapIdFromSourceSymbol(original.GetProperty("map").GetString()!)), "Four Island connection differs.");
        }
        Need(contract.GetProperty("externalBoundaries").GetProperty("connections").GetArrayLength() == 0 &&
            contract.GetProperty("externalBoundaries").GetProperty("warps").GetArrayLength() == 5 &&
            !maps.ContainsKey(new("map.four-island-pokemon-day-care")) && !maps.ContainsKey(new("map.four-island-loreleis-house")) &&
            !maps.Keys.Any(k => k.Value.StartsWith("map.five-island", StringComparison.Ordinal)), "Later interiors or islands were admitted.");
        Need(contract.GetProperty("excludedEncounterGates").GetArrayLength() == 0, "Original wild encounter tables were unexpectedly gated.");
        Need(constants["VAR_TEMP_1"] == 0x4001 && constants["HEAL_LOCATION_FOUR_ISLAND"] == 17 && constants["TRAINER_TEAM_ROCKET_GRUNT_45"] == 539,
            "Four Island source constants differ.");
        ValidateIce(source, contract, maps, constants);
        foreach (var row in source.Document("catalog.json").GetProperty("mapSymbols").EnumerateObject())
            Need(MapIdFromSourceSymbol(row.Name).Value == row.Value.GetString(), "Four Island map symbol mapping differs.");
    }
    private static void ValidateIce(SilphSourcePackage source, JsonElement contract, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants)
    {
        var upper = maps[new("map.four-island-icefall-cave-1f")];
        var lower = maps[new("map.four-island-icefall-cave-b1f")];
        var entrance = maps[new("map.four-island-icefall-cave-entrance")];
        void Cell(JsonElement row, PretWorldMap map)
        {
            var cell = map.GetSourceCell(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32());
            Need(cell is not null && cell.RawBlock == row.GetProperty("rawBlock").GetInt32() &&
                cell.RawAttributes == row.GetProperty("rawAttributes").GetUInt32() && cell.BehaviorId == row.GetProperty("behaviorId").GetInt32(),
                "Four Island field anchor differs from authenticated map cells.");
        }
        var pairs = contract.GetProperty("iceFallPairs").EnumerateArray().ToArray();
        Need(pairs.Length == 9 && pairs.Count(p => p.GetProperty("fallDestination").GetProperty("behaviorId").GetInt32() == 0x23) == 2,
            "Nine thin-ice anchors and two ice landings retained.");
        for (var i = 0; i < pairs.Length; i++)
        {
            var pair = pairs[i]; Cell(pair.GetProperty("source"), upper); Cell(pair.GetProperty("fallDestination"), lower);
            var before = pair.GetProperty("source"); var after = pair.GetProperty("fallDestination");
            Need(before.GetProperty("x").GetInt32() == after.GetProperty("x").GetInt32() && before.GetProperty("y").GetInt32() == after.GetProperty("y").GetInt32() &&
                before.GetProperty("behaviorId").GetInt32() == 0x26 && after.GetProperty("collision").GetInt32() == 0 && after.GetProperty("elevation").GetInt32() == 3 &&
                pair.GetProperty("flag").GetString() == "FLAG_TEMP_" + (i + 1) && pair.GetProperty("flagValue").GetInt32() == constants["FLAG_TEMP_" + (i + 1)],
                "Thin-ice temporary flag or same-coordinate fall differs.");
        }
        var lanes = contract.GetProperty("waterfallLanes").EnumerateArray().ToArray();
        Need(lanes.Length == 5, "Five original Waterfall lanes retained.");
        foreach (var lane in lanes)
        {
            var x = lane.GetProperty("x").GetInt32();
            Need(x is >= 15 and <= 19 && lane.GetProperty("firstY").GetInt32() == 14 && lane.GetProperty("lastY").GetInt32() == 20,
                "Waterfall extent differs.");
            Cell(lane.GetProperty("above"), entrance); Cell(lane.GetProperty("below"), entrance);
            var cells = lane.GetProperty("cells").EnumerateArray().ToArray(); Need(cells.Length == 7, "Waterfall lane length differs.");
            foreach (var cell in cells) { Cell(cell, entrance); Need(cell.GetProperty("behaviorId").GetInt32() == 0x13, "Waterfall behavior was flattened."); }
        }
        Need(contract.GetProperty("slidingIceCells").GetArrayLength() == 70, "Original sliding-ice count differs.");
        foreach (var cell in contract.GetProperty("slidingIceCells").EnumerateArray()) Cell(cell, lower);
        var attrs = source.Raw["data/tilesets/secondary/seafoam_islands/metatile_attributes.bin"];
        foreach (var patch in contract.GetProperty("iceMetatilePatches").EnumerateArray())
        {
            var id = patch.GetProperty("metatileId").GetInt32();
            var attribute = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(attrs.AsSpan((id - 640) * 4));
            Need(constants[patch.GetProperty("symbol").GetString()!] == id && id is 0x35A or 0x35B &&
                patch.GetProperty("rawAttributes").GetUInt32() == attribute && patch.GetProperty("preserveElevation").GetBoolean() &&
                patch.GetProperty("collision").GetInt32() == 0, "Dynamic ice metatile differs from source attributes.");
        }
        foreach (var row in contract.GetProperty("mapHeaders").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(row.GetProperty("map").GetString()!)];
            Need(row.GetProperty("allowCycling").GetBoolean() == map.Events.GetProperty("allow_cycling").GetBoolean() &&
                row.GetProperty("allowEscaping").GetBoolean() == map.Events.GetProperty("allow_escaping").GetBoolean() &&
                row.GetProperty("allowRunning").GetBoolean() == map.Events.GetProperty("allow_running").GetBoolean(), "Source movement header differs.");
        }
        Need(new[] { upper, lower, entrance, maps[new("map.four-island-icefall-cave-back")] }.All(m =>
            m.Events.GetProperty("allow_cycling").GetBoolean() && m.Events.GetProperty("allow_escaping").GetBoolean()), "Original cave cycling or Escape Rope permission removed.");
    }
}
