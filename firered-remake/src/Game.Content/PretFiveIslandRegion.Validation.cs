using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;
public sealed partial class PretFiveIslandRegion
{
    private static void ValidateContracts(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants)
    {
        var contract = source.Document("contracts.json");
        Need(contract.GetProperty("itemPickups").GetArrayLength() == 6 && contract.GetProperty("hiddenItems").GetArrayLength() == 2 &&
            source.Document("npc-source.json").GetProperty("graphics").GetArrayLength() == 13 &&
            source.Document("declarations.json").GetProperty("trainers").GetArrayLength() == 9 , "Five Island complete field inventory differs.");
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
        Need(topology.GetProperty("warps").GetArrayLength() == 9 && topology.GetProperty("connections").GetArrayLength() == 4, "Complete map topology differs.");
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
            ["MAP_FIVE_ISLAND_HOUSE1", "MAP_FIVE_ISLAND_HOUSE2", "MAP_FIVE_ISLAND_POKEMON_CENTER_2F"]) &&
            external.GetProperty("warps").GetArrayLength() == 3 && external.GetProperty("connections").GetArrayLength() == 2 &&
            external.GetProperty("connections").EnumerateArray().Select(c => c.GetProperty("source").GetProperty("map").GetString()).ToHashSet().SetEquals(
                ["MAP_FIVE_ISLAND_WATER_LABYRINTH", "MAP_FIVE_ISLAND_MEMORIAL_PILLAR"]), "Exact external boundaries differ.");
        Need(contract.GetProperty("excludedEncounterGates").GetArrayLength() == 0 && maps.Keys.All(k => k.Value.StartsWith("map.five-island", StringComparison.Ordinal)) &&
            !maps.ContainsKey(new("map.five-island-water-labyrinth")) && !maps.ContainsKey(new("map.five-island-memorial-pillar")), "Unadmitted geography or encounter gating differs.");
        ValidateWarehouse(source, contract, maps, constants);
        foreach (var row in source.Document("catalog.json").GetProperty("mapSymbols").EnumerateObject())
            Need(MapIdFromSourceSymbol(row.Name).Value == row.Value.GetString(), "Five Island map resolver differs.");
    }
    private static void ValidateWarehouse(SilphSourcePackage source, JsonElement contract, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants)
    {
        var blocks = source.Document("scripts.json").GetProperty("blocks").EnumerateArray().ToDictionary(b => b.GetProperty("label").GetString()!);
        void Block(JsonElement b) => Need(JsonElement.DeepEquals(b, blocks[b.GetProperty("label").GetString()!]), "Field script differs from authenticated source inventory.");
        string[] Lines(JsonElement b) => b.GetProperty("lines").EnumerateArray().Select(r => r.GetProperty("text").GetString()!.Split('@')[0].Trim()).Where(s => s.Length > 0).ToArray();
        void Cell(JsonElement row, PretWorldMap map)
        {
            var cell = map.GetSourceCell(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32());
            Need(cell is not null && cell.RawBlock == row.GetProperty("rawBlock").GetInt32() && cell.RawAttributes == row.GetProperty("rawAttributes").GetUInt32() &&
                cell.BehaviorId == row.GetProperty("behaviorId").GetInt32(), "Field anchor differs from source cell.");
        }
        void Tile(JsonElement tile, string tileset)
        {
            var id = tile.GetProperty("metatileId").GetInt32();
            var attr = BinaryPrimitives.ReadUInt32LittleEndian(source.Raw["data/tilesets/secondary/" + tileset + "/metatile_attributes.bin"].AsSpan((id - 640) * 4));
            Need(constants[tile.GetProperty("symbol").GetString()!] == id && tile.GetProperty("rawAttributes").GetUInt32() == attr &&
                tile.GetProperty("behaviorId").GetInt32() == (attr & 511), "Complete source tileset patch differs.");
        }
        var meadow = maps[new("map.five-island-meadow")]; var warehouse = maps[new("map.five-island-rocket-warehouse")];
        var door = contract.GetProperty("warehouseDoor"); Cell(door.GetProperty("sourceCell"), meadow);
        Need(door.GetProperty("x").GetInt32() == 12 && door.GetProperty("y").GetInt32() == 21 && door.GetProperty("openCollision").GetInt32() == 1 &&
            door.GetProperty("flag").GetString() == "FLAG_UNLOCKED_ROCKET_WAREHOUSE" && door.GetProperty("passwordFlag").GetString() == "FLAG_LEARNED_YES_NAH_CHANSEY", "Warehouse door coordinates or flags differ.");
        Need(door.GetProperty("metatiles").EnumerateArray().Select(t => t.GetProperty("metatileId").GetInt32()).SequenceEqual(new[] { 0x30B,0x2AF }) &&
            meadow.GetSourceCell(12, 21)!.MetatileId == 0x30B, "Locked/unlocked Warehouse door tiles differ.");
        foreach (var tile in door.GetProperty("metatiles").EnumerateArray()) Tile(tile, "sevii_islands_45");
        foreach (var key in new[] { "onLoad","entryScript","openScript","patchScript" }) Block(door.GetProperty(key));
        Need(Lines(door.GetProperty("patchScript")).SequenceEqual(new[] { "setmetatile 12, 21, METATILE_SeviiIslands45_RocketWarehouseDoor_Unlocked, 1","return" }), "Original door patch collision must remain one.");
        var arrows = contract.GetProperty("admin1Arrows");
        foreach (var row in arrows.GetProperty("originalCells").EnumerateArray()) Cell(row, warehouse);
        Tile(arrows.GetProperty("metatile"), "silph_co");
        Need(arrows.GetProperty("originalCells").GetArrayLength() == 2 && arrows.GetProperty("metatile").GetProperty("metatileId").GetInt32() == 0x2A0 &&
            arrows.GetProperty("collision").GetInt32() == 0 && arrows.GetProperty("trainer").GetString() == "TRAINER_TEAM_ROCKET_ADMIN" &&
            warehouse.GetSourceCell(26, 20)!.BehaviorId == 0x57 && warehouse.GetSourceCell(27, 20)!.BehaviorId == 0x57, "Admin1 original downward and replacement upward arrows differ.");
        foreach (var key in new[] { "onLoad","patchScript","defeatedScript" }) Block(arrows.GetProperty(key));
        Need(Lines(arrows.GetProperty("patchScript")).SequenceEqual(new[] { "setmetatile 26, 20, METATILE_SilphCo_Arrow_Up, 0","setmetatile 27, 20, METATILE_SilphCo_Arrow_Up, 0","return" }), "Original two-arrow edit differs.");
        Need(contract.GetProperty("spinTiles").GetArrayLength() == 135 && warehouse.Cells.Count(c => c.BehaviorId is >= 0x54 and <= 0x58) == 135, "Complete Warehouse spin geometry differs.");
        foreach (var row in contract.GetProperty("spinTiles").EnumerateArray()) Cell(row, warehouse);
        var recovery = contract.GetProperty("sapphireRecovery"); Block(recovery.GetProperty("script")); var recoveryLines = Lines(recovery.GetProperty("script"));
        Need(recovery.GetProperty("grantItem").GetString() == "ITEM_SAPPHIRE" && recovery.GetProperty("endpointFlag").GetString() == "FLAG_RECOVERED_SAPPHIRE" &&
            !recovery.GetProperty("laterCelioDeliveryAdmitted").GetBoolean() && Array.IndexOf(recoveryLines, "setflag FLAG_RECOVERED_SAPPHIRE") < Array.IndexOf(recoveryLines, "giveitem ITEM_SAPPHIRE"), "Gideon source reward order or bounded endpoint differs.");
        Block(contract.GetProperty("rocketDeparture").GetProperty("script"));
        var clone = contract.GetProperty("cloneObjects");
        Need(clone.GetArrayLength() == 1 && JsonElement.DeepEquals(clone[0].GetProperty("event"), maps[new("map.five-island")].Events.GetProperty("object_events")[2]) &&
            constants["LOCALID_FIVE_ISLAND_MEADOW_BORDER_TREE"] == 4 && meadow.Events.GetProperty("object_events")[3].GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_CUT_TREE", "Original off-map clone and target tree differ.");
        Need(source.Document("npc-source.json").GetProperty("invisibleActors").GetArrayLength() == 2 && constants["HEAL_LOCATION_FIVE_ISLAND"] == 18 &&
            constants["PLAYER_X_POS"] == constants["VAR_0x8004"] && constants["PLAYER_Y_POS"] == constants["VAR_0x8005"], "Invisible journal actors, heal18 or local variable aliases differ.");
        foreach (var row in contract.GetProperty("mapHeaders").EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(row.GetProperty("map").GetString()!)];
            Need(row.GetProperty("allowCycling").GetBoolean() == map.Events.GetProperty("allow_cycling").GetBoolean() &&
                row.GetProperty("allowEscaping").GetBoolean() == map.Events.GetProperty("allow_escaping").GetBoolean() && row.GetProperty("allowRunning").GetBoolean() == map.Events.GetProperty("allow_running").GetBoolean(), "Source movement header differs.");
        }
    }
}
