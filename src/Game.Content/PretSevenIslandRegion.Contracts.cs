using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretSevenIslandRegion
{
    private static SevenIslandFieldData LoadField(ImmutableDictionary<MapId, PretWorldMap> maps, JsonElement contract, ImmutableDictionary<string, int> constants)
    {
        var pickups = ImmutableArray.CreateBuilder<SevenIslandPickup>();
        foreach (string kind in new[] { "itemPickups","hiddenItems" })
        foreach (var row in contract.GetProperty(kind).EnumerateArray())
        {
            bool hidden = kind == "hiddenItems", renewable = row.GetProperty("renewable").GetBoolean();
            var e = row.GetProperty("event"); string map = row.GetProperty("map").GetString()!, item = row.GetProperty("item").GetString()!, flag = e.GetProperty("flag").GetString()!;
            var mapRow = maps.Values.Single(m => m.SourceMapSymbol == map);
            string path = "data/maps/" + mapRow.SourceMapName + "/map.json";
            pickups.Add(new(MapIdFromSourceSymbol(map), map, e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(),
                new("pret:" + item), item, constants[item], row.GetProperty("quantity").GetInt32(), flag, constants[flag], hidden,
                hidden && e.GetProperty("underfoot").GetBoolean(), hidden ? null : row.GetProperty("localId").GetInt32(), hidden ? null : e.GetProperty("script").GetString(),
                renewable, renewable ? row.GetProperty("renewalRarity").GetString() : null, renewable ? row.GetProperty("renewalIndex").GetInt32() : null,
                new(path, flag)));
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

}
