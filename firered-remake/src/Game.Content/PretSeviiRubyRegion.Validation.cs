using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;
public sealed partial class PretSeviiRubyRegion
{
    private static void ValidateContracts(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants)
    {
        var contract = source.Document("contracts.json");
        Need(contract.GetProperty("itemPickups").GetArrayLength() == 7 && contract.GetProperty("hiddenItems").GetArrayLength() == 2 &&
            source.Document("npc-source.json").GetProperty("graphics").GetArrayLength() == 19 &&
            source.Document("declarations.json").GetProperty("trainers").GetArrayLength() == 17, "Sevii Ruby source field scope differs.");
        foreach (var item in contract.GetProperty("itemPickups").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(item.GetProperty("map").GetString()!)];
            var localId = item.GetProperty("localId").GetInt32();
            var original = map.Events.GetProperty("object_events")[localId - 1];
            Need(JsonElement.DeepEquals(item.GetProperty("event"), original) && original.GetProperty("graphics_id").GetString() is "OBJ_EVENT_GFX_ITEM_BALL" or "OBJ_EVENT_GFX_RUBY" &&
                item.GetProperty("script").GetProperty("label").GetString() == original.GetProperty("script").GetString() &&
                constants.ContainsKey(original.GetProperty("flag").GetString()!), "Sevii Ruby visible pickup differs from source event.");
        }
        foreach (var item in contract.GetProperty("hiddenItems").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(item.GetProperty("map").GetString()!)];
            var original = item.GetProperty("event");
            Need(map.Events.GetProperty("bg_events").EnumerateArray().Count(e => JsonElement.DeepEquals(e, original)) == 1 &&
                constants.ContainsKey(original.GetProperty("item").GetString()!) && constants.ContainsKey(original.GetProperty("flag").GetString()!),
                "Sevii Ruby hidden pickup differs from source event.");
        }
        var rawMaps = source.Raw.Where(p => p.Key.StartsWith("data/maps/", StringComparison.Ordinal) && p.Key.EndsWith("/map.json", StringComparison.Ordinal))
            .Select(p => SilphSourcePackage.Json(p.Value)).ToDictionary(e => e.GetProperty("id").GetString()!, StringComparer.Ordinal);
        foreach (var row in contract.GetProperty("mapTopology").GetProperty("warps").EnumerateArray())
        {
            var original = rawMaps[row.GetProperty("sourceMap").GetString()!].GetProperty("warp_events")[row.GetProperty("sourceIndex").GetInt32()];
            var targetSymbol = original.GetProperty("dest_map").GetString()!;
            var target = rawMaps[targetSymbol].GetProperty("warp_events")[int.Parse(original.GetProperty("dest_warp_id").GetString()!, System.Globalization.CultureInfo.InvariantCulture)];
            Need(JsonElement.DeepEquals(original, row.GetProperty("source")) && JsonElement.DeepEquals(target, row.GetProperty("destination")) &&
                row.GetProperty("destinationInSlice").GetBoolean() == maps.ContainsKey(MapIdFromSourceSymbol(targetSymbol)), "Sevii Ruby warp boundary differs.");
        }
        foreach (var row in contract.GetProperty("mapTopology").GetProperty("connections").EnumerateArray())
        {
            var original = row.GetProperty("source"); var symbol = row.GetProperty("sourceMap").GetString()!;
            Need(rawMaps[symbol].GetProperty("connections").EnumerateArray().Any(e => JsonElement.DeepEquals(e, original)) &&
                row.GetProperty("destinationInSlice").GetBoolean() == maps.ContainsKey(MapIdFromSourceSymbol(original.GetProperty("map").GetString()!)), "Sevii Ruby connection differs.");
        }
        Need(contract.GetProperty("externalBoundaries").GetProperty("connections").GetArrayLength() == 1 &&
            contract.GetProperty("externalBoundaries").GetProperty("warps").GetArrayLength() == 3 &&
            !maps.ContainsKey(new("map.mt-ember-summit")) && !maps.ContainsKey(new("map.four-island")),
            "Later region/legendary boundaries were admitted.");
        Need(contract.GetProperty("excludedEncounterGates").GetArrayLength() == 0,
            "Original wild encounter tables were unexpectedly gated.");
        Need(constants["VAR_TEMP_1"] == 0x4001 && constants["HEAL_LOCATION_ONE_ISLAND"] == 14,
            "Ruby-region constants differ.");
        foreach (var row in source.Document("catalog.json").GetProperty("mapSymbols").EnumerateObject())
            Need(MapIdFromSourceSymbol(row.Name).Value == row.Value.GetString(), "Sevii Ruby map symbol mapping differs.");
    }
}
