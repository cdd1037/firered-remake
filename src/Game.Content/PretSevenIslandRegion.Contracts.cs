using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretSevenIslandRegion
{
    private static SevenIslandFieldData LoadField(PretCurrentWorld world, ImmutableDictionary<MapId, PretWorldMap> maps, JsonElement contract, ImmutableDictionary<string, int> constants)
    {
        PretWorldMap Local(string symbol) => maps.TryGetValue(world.Resolve(symbol), out var map)
            ? map : throw new InvalidDataException("Seven Island field source map is not mounted.");
        PretWorldMap Target(string symbol) => world.Maps.TryGetValue(world.Resolve(symbol), out var map)
            ? map : throw new InvalidDataException("Seven Island field target map is not selected.");
        static bool Matches(JsonElement actual, JsonElement subset) => subset.EnumerateObject()
            .All(p => actual.TryGetProperty(p.Name, out var value) && JsonElement.DeepEquals(value, p.Value));
        static JsonElement Actor(PretWorldMap map, int localId)
        {
            var actors = map.Events.GetProperty("object_events");
            Need(localId > 0 && localId <= actors.GetArrayLength(), "Seven Island actor endpoint is invalid.");
            return actors[localId - 1];
        }
        var pickups = ImmutableArray.CreateBuilder<SevenIslandPickup>();
        foreach (string kind in new[] { "itemPickups","hiddenItems" })
        foreach (var row in contract.GetProperty(kind).EnumerateArray())
        {
            bool hidden = kind == "hiddenItems", renewable = row.GetProperty("renewable").GetBoolean();
            var e = row.GetProperty("event"); string map = row.GetProperty("map").GetString()!, item = row.GetProperty("item").GetString()!, flag = e.GetProperty("flag").GetString()!;
            var mapRow = Local(map);
            Need(mapRow.GetSourceCell(e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32()) is not null &&
                (hidden ? mapRow.Events.GetProperty("bg_events").EnumerateArray().Any(a => Matches(a, e))
                    : Matches(Actor(mapRow, row.GetProperty("localId").GetInt32()), e)), "Seven Island pickup endpoint differs.");
            string path = "data/maps/" + mapRow.SourceMapName + "/map.json";
            pickups.Add(new(world.Resolve(map), map, e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(),
                new("pret:" + item), item, constants[item], row.GetProperty("quantity").GetInt32(), flag, constants[flag], hidden,
                hidden && e.GetProperty("underfoot").GetBoolean(), hidden ? null : row.GetProperty("localId").GetInt32(), hidden ? null : e.GetProperty("script").GetString(),
                renewable, renewable ? row.GetProperty("renewalRarity").GetString() : null, renewable ? row.GetProperty("renewalIndex").GetInt32() : null,
                new(path, flag)));
        }
        var key = contract.GetProperty("tanobyKey");
        var puzzleMap = Local(key.GetProperty("map").GetString()!);
        var switches = key.GetProperty("switches").EnumerateArray().Select(r =>
        {
            var e = r.GetProperty("event");
            Need(puzzleMap.GetSourceCell(e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32()) is not null &&
                puzzleMap.Events.GetProperty("coord_events").EnumerateArray().Any(a => Matches(a, e)), "Seven Island switch endpoint differs.");
            return new SevenIslandStrengthSwitch(r.GetProperty("index").GetInt32(), e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(),
                r.GetProperty("temporaryVariable").GetString()!, r.GetProperty("pressedValue").GetInt32(), e.GetProperty("script").GetString()!);
        }).ToImmutableArray();
        var boulders = key.GetProperty("boulders").EnumerateArray().Select(r =>
        {
            var e = r.GetProperty("event");
            Need(Matches(Actor(puzzleMap, r.GetProperty("localId").GetInt32()), e) &&
                puzzleMap.GetSourceCell(r.GetProperty("solvedX").GetInt32(), r.GetProperty("solvedY").GetInt32()) is not null,
                "Seven Island boulder endpoint differs.");
            return new SevenIslandStrengthBoulder(r.GetProperty("localId").GetInt32(), e.GetProperty("local_id").GetString()!, e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(),
                e.GetProperty("elevation").GetInt32(), r.GetProperty("solvedX").GetInt32(), r.GetProperty("solvedY").GetInt32());
        }).ToImmutableArray();
        string solvedFlag = key.GetProperty("solvedFlag").GetString()!;
        var puzzle = new SevenIslandStrengthPuzzle(world.Resolve(key.GetProperty("map").GetString()!), solvedFlag, constants[solvedFlag], key.GetProperty("countVariable").GetString()!,
            key.GetProperty("requiredCount").GetInt32(), key.GetProperty("strengthBehaviorId").GetInt32(), switches, boulders);
        var ferry = contract.GetProperty("ferryLanding");
        Need(Local(ferry.GetProperty("map").GetString()!).GetSourceCell(ferry.GetProperty("x").GetInt32(), ferry.GetProperty("y").GetInt32()) is not null,
            "Seven Island ferry endpoint differs.");
        var landing = new SevenIslandFerryLanding(world.Resolve(ferry.GetProperty("map").GetString()!), ferry.GetProperty("x").GetInt32(), ferry.GetProperty("y").GetInt32(), Direction.South);
        var heal = contract.GetProperty("healLocation"); var h = heal.GetProperty("declaration");
        var respawn = Target(h.GetProperty("respawn_map").GetString()!);
        Need(Local(h.GetProperty("map").GetString()!).GetSourceCell(h.GetProperty("x").GetInt32(), h.GetProperty("y").GetInt32()) is not null &&
            respawn.GetSourceCell(heal.GetProperty("blackoutX").GetInt32(), heal.GetProperty("blackoutY").GetInt32()) is not null &&
            respawn.Events.GetProperty("object_events").EnumerateArray().Count(a =>
                a.TryGetProperty("local_id", out var id) && id.GetString() == h.GetProperty("respawn_npc").GetString() &&
                a.GetProperty("graphics_id").GetString() == "OBJ_EVENT_GFX_NURSE") == 1, "Seven Island healing endpoint differs.");
        var healing = new SevenIslandHealLocation(heal.GetProperty("sourceId").GetInt32(), world.Resolve(h.GetProperty("map").GetString()!), h.GetProperty("x").GetInt32(), h.GetProperty("y").GetInt32(),
            world.Resolve(h.GetProperty("respawn_map").GetString()!), h.GetProperty("respawn_npc").GetString()!, heal.GetProperty("blackoutX").GetInt32(), heal.GetProperty("blackoutY").GetInt32(), Direction.North);
        foreach (var row in contract.GetProperty("clones").EnumerateArray())
        {
            var actor = Actor(Local(row.GetProperty("map").GetString()!), row.GetProperty("localId").GetInt32());
            var declaration = row.GetProperty("declaration");
            var target = Actor(Target(row.GetProperty("targetMap").GetString()!), row.GetProperty("targetLocalId").GetInt32());
            // Clones may deliberately lie outside the source map (the tutor has Y = -2).
            Need(actor.GetProperty("type").GetString() == "clone" && Matches(actor, declaration) &&
                world.Resolve(actor.GetProperty("target_map").GetString()!) == world.Resolve(row.GetProperty("targetMap").GetString()!) &&
                target.TryGetProperty("local_id", out var targetId) && targetId.GetString() == declaration.GetProperty("target_local_id").GetString(),
                "Seven Island clone endpoint differs.");
        }
        var clones = contract.GetProperty("clones").EnumerateArray().Select(r => new SevenIslandCloneActor(world.Resolve(r.GetProperty("map").GetString()!), r.GetProperty("localId").GetInt32(),
            r.GetProperty("declaration").GetProperty("x").GetInt32(), r.GetProperty("declaration").GetProperty("y").GetInt32(), world.Resolve(r.GetProperty("targetMap").GetString()!),
            r.GetProperty("targetLocalId").GetInt32(), r.GetProperty("declaration").GetProperty("target_local_id").GetString()!)).ToImmutableArray();
        return new(pickups.ToImmutable(), puzzle, landing, healing, clones);
    }

}
