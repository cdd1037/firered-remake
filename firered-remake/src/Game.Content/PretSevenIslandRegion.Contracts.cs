using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretSevenIslandRegion
{
    private static SevenIslandFieldData LoadField(SilphSourcePackage source, ImmutableDictionary<string, int> constants)
    {
        var contract = source.Document("contracts.json");
        var files = source.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var pickups = ImmutableArray.CreateBuilder<SevenIslandPickup>();
        foreach (string kind in new[] { "itemPickups", "hiddenItems" })
        foreach (var row in contract.GetProperty(kind).EnumerateArray())
        {
            bool hidden = kind == "hiddenItems", renewable = row.GetProperty("renewable").GetBoolean();
            var e = row.GetProperty("event"); string map = row.GetProperty("map").GetString()!, item = row.GetProperty("item").GetString()!, flag = e.GetProperty("flag").GetString()!;
            var catalog = source.Document("catalog.json").GetProperty("maps").EnumerateArray().Single(m => m.GetProperty("sourceMapSymbol").GetString() == map);
            string path = "data/maps/" + catalog.GetProperty("sourceName").GetString() + "/map.json";
            pickups.Add(new(MapIdFromSourceSymbol(map), map, e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(),
                new("pret:" + item), item, constants[item], row.GetProperty("quantity").GetInt32(), flag, constants[flag], hidden,
                hidden && e.GetProperty("underfoot").GetBoolean(), hidden ? null : row.GetProperty("localId").GetInt32(), hidden ? null : e.GetProperty("script").GetString(),
                renewable, renewable ? row.GetProperty("renewalRarity").GetString() : null, renewable ? row.GetProperty("renewalIndex").GetInt32() : null,
                new(path, flag, files[path].Sha256)));
        }
        var key = contract.GetProperty("tanobyKey");
        var switches = key.GetProperty("switches").EnumerateArray().Select(r =>
        {
            var e = r.GetProperty("event");
            return new SevenIslandStrengthSwitch(r.GetProperty("index").GetInt32(), e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(),
                r.GetProperty("temporaryVariable").GetString()!, r.GetProperty("pressedValue").GetInt32(), e.GetProperty("script").GetString()!);
        }).ToImmutableArray();
        var boulders = key.GetProperty("boulders").EnumerateArray().Select(r =>
        {
            var e = r.GetProperty("event");
            return new SevenIslandStrengthBoulder(r.GetProperty("localId").GetInt32(), e.GetProperty("local_id").GetString()!, e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(),
                e.GetProperty("elevation").GetInt32(), r.GetProperty("solvedX").GetInt32(), r.GetProperty("solvedY").GetInt32());
        }).ToImmutableArray();
        string solvedFlag = key.GetProperty("solvedFlag").GetString()!;
        var puzzle = new SevenIslandStrengthPuzzle(MapIdFromSourceSymbol(key.GetProperty("map").GetString()!), solvedFlag, constants[solvedFlag], key.GetProperty("countVariable").GetString()!,
            key.GetProperty("requiredCount").GetInt32(), key.GetProperty("strengthBehaviorId").GetInt32(), switches, boulders);
        var ferry = contract.GetProperty("ferryLanding");
        var landing = new SevenIslandFerryLanding(MapIdFromSourceSymbol(ferry.GetProperty("map").GetString()!), ferry.GetProperty("x").GetInt32(), ferry.GetProperty("y").GetInt32(), Direction.South);
        var heal = contract.GetProperty("healLocation"); var h = heal.GetProperty("declaration");
        var healing = new SevenIslandHealLocation(heal.GetProperty("sourceId").GetInt32(), MapIdFromSourceSymbol(h.GetProperty("map").GetString()!), h.GetProperty("x").GetInt32(), h.GetProperty("y").GetInt32(),
            MapIdFromSourceSymbol(h.GetProperty("respawn_map").GetString()!), h.GetProperty("respawn_npc").GetString()!, heal.GetProperty("blackoutX").GetInt32(), heal.GetProperty("blackoutY").GetInt32(), Direction.North);
        var clones = contract.GetProperty("clones").EnumerateArray().Select(r => new SevenIslandCloneActor(MapIdFromSourceSymbol(r.GetProperty("map").GetString()!), r.GetProperty("localId").GetInt32(),
            r.GetProperty("declaration").GetProperty("x").GetInt32(), r.GetProperty("declaration").GetProperty("y").GetInt32(), MapIdFromSourceSymbol(r.GetProperty("targetMap").GetString()!),
            r.GetProperty("targetLocalId").GetInt32(), r.GetProperty("declaration").GetProperty("target_local_id").GetString()!)).ToImmutableArray();
        return new(pickups.ToImmutable(), puzzle, landing, healing, clones);
    }

    private static void ValidateContracts(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants)
    {
        var contract = source.Document("contracts.json");
        Need(contract.GetProperty("itemPickups").GetArrayLength() == 4 && contract.GetProperty("hiddenItems").GetArrayLength() == 9 &&
            maps.Values.Sum(m => m.Events.GetProperty("object_events").GetArrayLength()) == 59, "Seven Island object/pickup scope differs.");
        foreach (string kind in new[] { "itemPickups", "hiddenItems" })
        foreach (var row in contract.GetProperty(kind).EnumerateArray())
        {
            var map = maps[MapIdFromSourceSymbol(row.GetProperty("map").GetString()!)]; var e = row.GetProperty("event"); bool hidden = kind == "hiddenItems";
            var original = hidden ? map.Events.GetProperty("bg_events").EnumerateArray().Single(x => x.TryGetProperty("flag", out var f) && f.GetString() == e.GetProperty("flag").GetString()) :
                map.Events.GetProperty("object_events")[row.GetProperty("localId").GetInt32() - 1];
            Need(JsonElement.DeepEquals(e, original) && constants.ContainsKey(row.GetProperty("item").GetString()!) && constants.ContainsKey(e.GetProperty("flag").GetString()!), "Seven Island pickup differs from raw source.");
            if (hidden) Need(e.GetProperty("item").GetString() == row.GetProperty("item").GetString() && e.GetProperty("quantity").GetInt32() == row.GetProperty("quantity").GetInt32(), "Hidden pickup quantity/item differs.");
            else Need(e.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_ITEM_BALL" && row.GetProperty("script").GetProperty("label").GetString() == e.GetProperty("script").GetString(), "Visible pickup script differs.");
        }
        var rawMaps = source.Raw.Where(p => p.Key.StartsWith("data/maps/", StringComparison.Ordinal) && p.Key.EndsWith("/map.json", StringComparison.Ordinal))
            .Select(p => SilphSourcePackage.Json(p.Value)).ToDictionary(e => e.GetProperty("id").GetString()!, StringComparer.Ordinal);
        var topology = contract.GetProperty("mapTopology");
        foreach (var row in topology.GetProperty("warps").EnumerateArray())
        {
            string symbol = row.GetProperty("sourceMap").GetString()!; int index = row.GetProperty("sourceIndex").GetInt32();
            var original = rawMaps[symbol].GetProperty("warp_events")[index]; string targetSymbol = original.GetProperty("dest_map").GetString()!;
            var target = rawMaps[targetSymbol].GetProperty("warp_events")[int.Parse(original.GetProperty("dest_warp_id").GetString()!, CultureInfo.InvariantCulture)];
            Need(JsonElement.DeepEquals(original, row.GetProperty("source")) && JsonElement.DeepEquals(target, row.GetProperty("destination")) &&
                row.GetProperty("destinationInSlice").GetBoolean() == maps.ContainsKey(MapIdFromSourceSymbol(targetSymbol)), "Original Seven Island warp changed.");
        }
        foreach (var row in topology.GetProperty("connections").EnumerateArray())
            Need(rawMaps[row.GetProperty("sourceMap").GetString()!].GetProperty("connections").EnumerateArray().Any(x => JsonElement.DeepEquals(x, row.GetProperty("source"))) &&
                row.GetProperty("destinationInSlice").GetBoolean(), "Original Seven Island connection changed.");
        var external = contract.GetProperty("externalBoundaries");
        Need(external.GetProperty("warps").GetArrayLength() == 3 && external.GetProperty("connections").GetArrayLength() == 0 &&
            external.GetProperty("warps").EnumerateArray().Select(w => w.GetProperty("source").GetProperty("dest_map").GetString()).ToHashSet().SetEquals(
                ["MAP_TRAINER_TOWER_LOBBY", "MAP_SEVEN_ISLAND_HOUSE_ROOM2", "MAP_SEVEN_ISLAND_POKEMON_CENTER_2F"]), "Unsupported Seven Island boundary changed.");
        var key = contract.GetProperty("tanobyKey"); var keyMap = maps[MapIdFromSourceSymbol(key.GetProperty("map").GetString()!)];
        var expected = new[] { (7, 2), (4, 4), (10, 4), (5, 6), (9, 6), (5, 8), (9, 8) };
        Need(key.GetProperty("switches").GetArrayLength() == 7 && key.GetProperty("boulders").GetArrayLength() == 7 &&
            key.GetProperty("solvedFlag").GetString() == "FLAG_SYS_UNLOCKED_TANOBY_RUINS" && key.GetProperty("countVariable").GetString() == "VAR_TEMP_8", "Tanoby puzzle scope differs.");
        for (int i = 0; i < 7; i++)
        {
            var row = key.GetProperty("switches")[i]; var e = row.GetProperty("event"); var cell = keyMap.GetSourceCell(expected[i].Item1, expected[i].Item2);
            Need(JsonElement.DeepEquals(e, keyMap.Events.GetProperty("coord_events")[row.GetProperty("sourceEventIndex").GetInt32()]) && (e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32()) == expected[i] &&
                e.GetProperty("var_value").GetString() == "99" && cell is { BehaviorId: 0x20, Elevation: 3 } && row.GetProperty("temporaryVariable").GetString() == "VAR_TEMP_" + (i + 1) &&
                row.GetProperty("pressedValue").GetInt32() == 100 && JsonElement.DeepEquals(key.GetProperty("boulders")[i].GetProperty("event"), keyMap.Events.GetProperty("object_events")[i]), "Tanoby boulder/switch source changed.");
        }
        var renewal = contract.GetProperty("renewableHiddenItems");
        Need(renewal.GetProperty("stepThreshold").GetInt32() == RenewableHiddenItemRules.StepThreshold && renewal.GetProperty("originalGlobalGroupCount").GetInt32() == RenewableHiddenItemRules.Groups.Length &&
            source.Files.Single(f => f.Path == RenewableHiddenItemRules.SourcePath).Sha256 == RenewableHiddenItemRules.SourceSha256, "Global renewal source/1500-step policy differs.");
        foreach (var row in renewal.GetProperty("groups").EnumerateArray())
        {
            var group = RenewableHiddenItemRules.Groups.Single(g => g.MapSymbol == row.GetProperty("map").GetString());
            foreach (var (name, flags) in new[] { ("rare", group.Rare), ("uncommon", group.Uncommon), ("common", group.Common) })
                Need(row.GetProperty(name).EnumerateArray().Select(x => x.GetString()).SequenceEqual(flags), "Renewable source group or ordering differs.");
        }
        Need(contract.GetProperty("hiddenItems").EnumerateArray().Count(x => x.GetProperty("renewable").GetBoolean()) == 6, "Six renewable items required.");
        var clone = contract.GetProperty("clones").EnumerateArray().Single();
        Need(JsonElement.DeepEquals(clone.GetProperty("declaration"), rawMaps[clone.GetProperty("map").GetString()!].GetProperty("object_events")[clone.GetProperty("localId").GetInt32() - 1]) &&
            JsonElement.DeepEquals(clone.GetProperty("target"), rawMaps[clone.GetProperty("targetMap").GetString()!].GetProperty("object_events")[clone.GetProperty("targetLocalId").GetInt32() - 1]), "Source tutor clone differs.");
        foreach (var row in source.Document("catalog.json").GetProperty("mapSymbols").EnumerateObject())
            Need(MapIdFromSourceSymbol(row.Name).Value == row.Value.GetString(), "Seven Island map resolver differs.");
    }
}
