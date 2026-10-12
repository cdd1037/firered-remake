using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;
public sealed partial class PretFirstSeviiRegion
{
    private static void ValidateContracts(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants)
    {
        var contract = source.Document("contracts.json");
        Need(contract.GetProperty("itemPickups").GetArrayLength() == 5 && contract.GetProperty("hiddenItems").GetArrayLength() == 17 &&
            source.Document("npc-source.json").GetProperty("graphics").GetArrayLength() == 32 &&
            source.Document("declarations.json").GetProperty("trainers").GetArrayLength() == 10, "First-Sevii source field scope differs.");
        foreach (var item in contract.GetProperty("itemPickups").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(item.GetProperty("map").GetString()!)];
            var localId = item.GetProperty("localId").GetInt32();
            var original = map.Events.GetProperty("object_events")[localId - 1];
            Need(JsonElement.DeepEquals(item.GetProperty("event"), original) && original.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_ITEM_BALL" &&
                item.GetProperty("script").GetProperty("label").GetString() == original.GetProperty("script").GetString() &&
                constants.ContainsKey(original.GetProperty("flag").GetString()!), "First-Sevii visible pickup differs from source event.");
        }
        foreach (var item in contract.GetProperty("hiddenItems").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(item.GetProperty("map").GetString()!)];
            var original = item.GetProperty("event");
            Need(map.Events.GetProperty("bg_events").EnumerateArray().Count(e => JsonElement.DeepEquals(e, original)) == 1 &&
                constants.ContainsKey(original.GetProperty("item").GetString()!) && constants.ContainsKey(original.GetProperty("flag").GetString()!),
                "First-Sevii hidden pickup differs from source event.");
        }
        var rawMaps = source.Raw.Where(p => p.Key.StartsWith("data/maps/", StringComparison.Ordinal) && p.Key.EndsWith("/map.json", StringComparison.Ordinal))
            .Select(p => SilphSourcePackage.Json(p.Value)).ToDictionary(e => e.GetProperty("id").GetString()!, StringComparer.Ordinal);
        foreach (var row in contract.GetProperty("mapTopology").GetProperty("warps").EnumerateArray())
        {
            var original = rawMaps[row.GetProperty("sourceMap").GetString()!].GetProperty("warp_events")[row.GetProperty("sourceIndex").GetInt32()];
            var targetSymbol = original.GetProperty("dest_map").GetString()!;
            var target = rawMaps[targetSymbol].GetProperty("warp_events")[int.Parse(original.GetProperty("dest_warp_id").GetString()!, System.Globalization.CultureInfo.InvariantCulture)];
            Need(JsonElement.DeepEquals(original, row.GetProperty("source")) && JsonElement.DeepEquals(target, row.GetProperty("destination")) &&
                row.GetProperty("destinationInSlice").GetBoolean() == maps.ContainsKey(MapIdFromSourceSymbol(targetSymbol)), "First-Sevii warp boundary differs.");
        }
        foreach (var row in contract.GetProperty("mapTopology").GetProperty("connections").EnumerateArray())
        {
            var original = row.GetProperty("source"); var symbol = row.GetProperty("sourceMap").GetString()!;
            Need(rawMaps[symbol].GetProperty("connections").EnumerateArray().Any(e => JsonElement.DeepEquals(e, original)) &&
                row.GetProperty("destinationInSlice").GetBoolean() == maps.ContainsKey(MapIdFromSourceSymbol(original.GetProperty("map").GetString()!)), "First-Sevii connection differs.");
        }
        Need(contract.GetProperty("externalBoundaries").GetProperty("connections").GetArrayLength() == 3 &&
            !maps.ContainsKey(new("map.three-island-dunsparce-tunnel")) && !maps.ContainsKey(new("map.one-island-kindle-road")) &&
            !maps.ContainsKey(new("map.two-island-cape-brink")), "Later-route boundary was admitted as a first-visit map.");
        var gate = contract.GetProperty("excludedEncounterGates")[0];
        Need(contract.GetProperty("excludedEncounterGates").GetArrayLength() == 1 && gate.GetProperty("map").GetString() == "MAP_THREE_ISLAND_PORT" &&
            gate.GetProperty("species").GetString() == "SPECIES_DUNSPARCE" && gate.GetProperty("gate").GetString() == "IsNationalPokedexEnabled" &&
            Encoding.UTF8.GetString(source.Raw[gate.GetProperty("sourcePath").GetString()!]).Contains("specialvar VAR_RESULT, IsNationalPokedexEnabled", StringComparison.Ordinal),
            "Dunsparce's original National-Dex gate differs.");
        var timing = contract.GetProperty("initialTiming");
        Need(timing.GetProperty("badge").GetString() == "FLAG_BADGE07_GET" && !timing.GetProperty("requiresGameClear").GetBoolean() &&
            !timing.GetProperty("requiresNationalDex").GetBoolean(), "First-Sevii initial timing differs.");
        Need(constants["VAR_TEMP_1"] == 0x4001 && constants["FLAG_SYS_PC_STORAGE_DISABLED"] == 0x841 &&
            constants["HEAL_LOCATION_ONE_ISLAND"] == 14 && constants["HEAL_LOCATION_TWO_ISLAND"] == 15 &&
            constants["HEAL_LOCATION_THREE_ISLAND"] == 16, "First-Sevii source runtime constants differ.");
        foreach (var row in source.Document("catalog.json").GetProperty("mapSymbols").EnumerateObject())
            Need(MapIdFromSourceSymbol(row.Name).Value == row.Value.GetString(), "First-Sevii map symbol mapping differs.");
    }
}
