using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

internal static class CurrentEscapeRoutes
{
    public static EscapeRopePolicy Create(IReadOnlyDictionary<MapId, PretWorldMap> sourceMaps, PretStoryCompilation? scripts = null)
    {
        var maps = sourceMaps.Keys.ToHashSet();
        var exits = ImmutableDictionary.CreateBuilder<MapId, ImmutableArray<EscapeRopeDestination>>();
        var entrances = ImmutableArray.CreateBuilder<EscapeRopeEntrance>();
        EscapeRopeDestination Exit(string map, int x, int y) => new(new("map." + map), x, y);
        void Dungeon(string[] names, params EscapeRopeDestination[] destinations)
        {
            foreach (string name in names)
            {
                var id = new MapId("map." + name);
                if (maps.Contains(id)) exits.Add(id, destinations.Where(d => maps.Contains(d.MapId)).ToImmutableArray());
            }
        }
        void Entrance(string from, string to, int x, int y, Direction facing = Direction.North)
        {
            var source = new MapId("map." + from); var destination = new MapId("map." + to);
            if (maps.Contains(source) && maps.Contains(destination))
                entrances.Add(new(new(source, x, y, 3, facing), destination, new(source, x, y + (facing == Direction.South ? 0 : 1))));
        }
        Dungeon(["mt-moon-1f", "mt-moon-b1f", "mt-moon-b2f"], Exit("route-4", 19, 6), Exit("route-4", 32, 6));
        Entrance("route-4", "mt-moon-1f", 19, 5); Entrance("route-4", "mt-moon-b1f", 32, 5);
        Dungeon(["viridian-forest"], Exit("route-2", 5, 13), Exit("route-2", 6, 13), Exit("route-2", 5, 52), Exit("route-2", 6, 52));
        foreach (int x in new[] {5, 6})
        {
            Entrance("route-2", "route-2-forest-south-gate", x, 51);
            Entrance("route-2", "route-2-forest-north-gate", x, 13, Direction.South);
        }
        Dungeon(["digletts-cave-south-entrance", "digletts-cave-b1f", "digletts-cave-north-entrance"], Exit("route-11", 6, 8), Exit("route-2", 17, 12));
        Entrance("route-11", "digletts-cave-south-entrance", 6, 7); Entrance("route-2", "digletts-cave-north-entrance", 17, 11);
        Dungeon(["rock-tunnel-1f", "rock-tunnel-b1f"], Exit("route-10", 8, 20), Exit("route-10", 8, 58));
        Entrance("route-10", "rock-tunnel-1f", 8, 19); Entrance("route-10", "rock-tunnel-1f", 8, 57);
        Dungeon(Enumerable.Range(1, 7).Select(n => $"pokemon-tower-{n}f").ToArray(), Exit("lavender-town", 18, 7));
        Entrance("lavender-town", "pokemon-tower-1f", 18, 6);
        Dungeon(Enumerable.Range(1, 11).Select(n => $"silph-co-{n}f").ToArray(), Exit("saffron-city", 33, 31));
        Entrance("saffron-city", "silph-co-1f", 33, 30);
        Dungeon(["pokemon-mansion-1f", "pokemon-mansion-2f", "pokemon-mansion-3f", "pokemon-mansion-b1f"], Exit("cinnabar-island", 8, 4));
        Entrance("cinnabar-island", "pokemon-mansion-1f", 8, 3);
        Dungeon(["three-island-berry-forest"], EscapeRopeRules.ForestDestination);
        Dungeon(["victory-road-1f", "victory-road-2f", "victory-road-3f"], Exit("route-23", 5, 29), Exit("route-23", 18, 29));
        Entrance("route-23", "victory-road-1f", 5, 28); Entrance("route-23", "victory-road-2f", 18, 28);
        Dungeon(["mt-ember-ruby-path-1f", "mt-ember-ruby-path-b1f", "mt-ember-ruby-path-b2f", "mt-ember-ruby-path-b3f", "mt-ember-ruby-path-b4f", "mt-ember-ruby-path-b5f", "mt-ember-ruby-path-b1f-stairs", "mt-ember-ruby-path-b2f-stairs"], Exit("mt-ember-exterior", 42, 40));
        Entrance("mt-ember-exterior", "mt-ember-ruby-path-1f", 42, 39);
        Dungeon(["four-island-icefall-cave-entrance", "four-island-icefall-cave-1f", "four-island-icefall-cave-b1f", "four-island-icefall-cave-back"], Exit("four-island", 38, 13));
        Entrance("four-island", "four-island-icefall-cave-entrance", 38, 12);
        Dungeon(DottedHoleSource.Maps.Keys.Select(m => m.Value[4..]).ToArray(), Exit("six-island-ruin-valley", 24, 25));
        Entrance("six-island-ruin-valley", DottedHoleSource.FirstFloor.Value[4..], 24, 24);
        Dungeon(["five-island-rocket-warehouse"], Exit("five-island-meadow", 12, 22));
        Entrance("five-island-meadow", "five-island-rocket-warehouse", 12, 21);
        Dungeon(["power-plant"], Exit("route-10", 7, 41), Exit("route-10", 2, 38));
        Entrance("route-10", "power-plant", 7, 40); Entrance("route-10", "power-plant", 2, 37, Direction.East);
        AddSourceEntrances(sourceMaps, exits, entrances);
        foreach (var map in sourceMaps.Values.Where(m => scripts is not null && m.Events.GetProperty("allow_escaping").GetBoolean()))
        {
            var path = "data/maps/" + map.SourceMapName + "/scripts.inc";
            var scripted = scripts!.Programs.Values.SelectMany(p => p.Instructions)
                .Where(i => i.Source.Path == path && i.Opcode == "setescapewarp")
                .Select(i =>
                {
                    if (i.Args.Length is not (3 or 4) || i.Args.Length == 4 && i.Args[1] != "255")
                        throw new InvalidDataException("escape_script_shape");
                    var target = sourceMaps.Values.SingleOrDefault(m => m.SourceMapSymbol == i.Args[0])
                        ?? throw new InvalidDataException("escape_script_map");
                    int Number(string value) => int.TryParse(value, out var number) ? number :
                        scripts.TryConstant(value, out number) ? number : throw new InvalidDataException("escape_script_coordinate");
                    int x = Number(i.Args[^2]), y = Number(i.Args[^1]);
                    if (target.GetTile(x, y) is not { Blocked: false }) throw new InvalidDataException("escape_script_tile");
                    return new EscapeRopeDestination(target.Id, x, y);
                }).Distinct().ToImmutableArray();
            if (!scripted.IsEmpty) exits[map.Id] = scripted;
        }
        return new(exits.ToImmutable(), entrances.ToImmutable());
    }
    // UpdateEscapeWarp records an outdoor -> indoor transition at the actual
    // entrance tile, with y + (facing != south). The allow_escaping header then
    // governs use. Existing explicit routes retain their exceptional source rules.
    private static void AddSourceEntrances(IReadOnlyDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<MapId, ImmutableArray<EscapeRopeDestination>>.Builder exits,
        ImmutableArray<EscapeRopeEntrance>.Builder entrances)
    {
        static bool Outdoors(PretWorldMap map) => map.Events.GetProperty("map_type").GetString() is
            "MAP_TYPE_ROUTE" or "MAP_TYPE_TOWN" or "MAP_TYPE_UNDERWATER" or "MAP_TYPE_CITY" or "MAP_TYPE_OCEAN_ROUTE";
        var allowed = maps.Values.Where(m => !Outdoors(m) && m.Events.GetProperty("allow_escaping").GetBoolean())
            .ToDictionary(m => m.Id);
        var remaining = allowed.Keys.Where(id => !exits.ContainsKey(id)).ToHashSet();
        while (remaining.Count != 0)
        {
            var component = new HashSet<MapId>();
            var pending = new Queue<MapId>(); pending.Enqueue(remaining.First());
            while (pending.TryDequeue(out var id))
            {
                if (!component.Add(id)) continue;
                foreach (var warp in allowed[id].FieldWarps)
                    if (allowed.ContainsKey(warp.DestinationMapId) && !exits.ContainsKey(warp.DestinationMapId)) pending.Enqueue(warp.DestinationMapId);
                foreach (var other in allowed.Values)
                    if (!exits.ContainsKey(other.Id) && other.FieldWarps.Any(w => w.DestinationMapId == id)) pending.Enqueue(other.Id);
            }
            remaining.ExceptWith(component);
            var found = new List<EscapeRopeEntrance>();
            foreach (var outside in maps.Values.Where(Outdoors))
            foreach (var warp in outside.FieldWarps.Where(w => component.Contains(w.DestinationMapId)))
            {
                var behavior = outside.SourceBehaviorAt(warp.X, warp.Y);
                foreach (var facing in Enum.GetValues<Direction>())
                {
                    bool arrow = behavior is >= 0x62 and <= 0x65;
                    if (arrow && facing != (behavior switch { 0x62 => Direction.East, 0x63 => Direction.West, 0x64 => Direction.North, _ => Direction.South })) continue;
                    if (!arrow && behavior is not (0x60 or 0x61 or 0x69)) continue;
                    if (behavior == 0x69 && facing != Direction.North) continue;
                    var (dx, dy) = facing switch { Direction.North => (0, -1), Direction.South => (0, 1), Direction.East => (1, 0), _ => (-1, 0) };
                    var approach = outside.GetTile(warp.X - (arrow ? 0 : dx), warp.Y - (arrow ? 0 : dy));
                    var entry = outside.GetTile(warp.X, warp.Y);
                    int exitY = warp.Y + (facing == Direction.South ? 0 : 1);
                    var exit = outside.GetTile(warp.X, exitY);
                    if (approach is not { Blocked: false } || entry is not { Blocked: false } || exit is not { Blocked: false } ||
                        entry.Elevation is not (0 or 15) && approach.Elevation != 0 && entry.Elevation != approach.Elevation) continue;
                    found.Add(new(new(outside.Id, warp.X, warp.Y, approach.Elevation, facing), warp.DestinationMapId,
                        new(outside.Id, warp.X, exitY)));
                }
            }
            // No inferred fallback for a dungeon without a source-compatible exterior entrance.
            if (found.Count == 0) continue;
            var destinations = found.Select(e => e.Exit).Distinct().ToImmutableArray();
            foreach (var id in component) exits.Add(id, destinations);
            entrances.AddRange(found);
        }
    }

}
