using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Roaming;

namespace Game.Content;

/// <summary>Original map-group/member indexes, including distinct interior groups, for player history.</summary>
public static class PretCelioMapNumbers
{
    public const string AssetPath = PretSourcePaths.FireRedRawDirectory + "/data/maps/map_groups.json";

    public static ImmutableDictionary<string, RoamerMap> LoadSourceTable(string root)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, AssetPath));
        using var document = JsonDocument.Parse(bytes);
        var source = document.RootElement;
        var groups = source.GetProperty("group_order").EnumerateArray().Select(x => x.GetString()!).ToArray();
        if (groups.Length != 43 || groups.Distinct(StringComparer.Ordinal).Count() != groups.Length ||
            source.EnumerateObject().Count() != groups.Length + 2 ||
            !source.TryGetProperty("connections_include_order", out var connections) || connections.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("celio_map_groups_scope");
        var table = ImmutableDictionary.CreateBuilder<string, RoamerMap>(StringComparer.Ordinal);
        for (int group = 0; group < groups.Length; group++)
        {
            int number = 0;
            foreach (var element in source.GetProperty(groups[group]).EnumerateArray())
            {
                string name = element.GetString() ?? throw new InvalidDataException("celio_map_name");
                if (number >= 256 || !table.TryAdd(name, new(group, number++)))
                    throw new InvalidDataException("celio_map_numbers_duplicate_or_range");
            }
        }
        if (table.Count != 425) throw new InvalidDataException("celio_map_numbers_count");
        return table.ToImmutable();
    }

    public static ImmutableDictionary<MapId, RoamerMap> Load(string root, IReadOnlyDictionary<MapId, PretWorldMap> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        return ProjectSource(root, maps);
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendCeruleanCave(string root,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, RoamerMap> predecessor)
    {
        ArgumentNullException.ThrowIfNull(maps); ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.Count != 254 || predecessor.Keys.Any(id => !maps.ContainsKey(id)) ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(new[] {
                new MapId("map.cerulean-cave-1f"),new MapId("map.cerulean-cave-2f"),new MapId("map.cerulean-cave-b1f") }))
            throw new InvalidDataException("cerulean_cave_map_numbers_scope");
        var current = ProjectSource(root, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value))
            throw new InvalidDataException("cerulean_cave_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendSevenIsland(string root,
        IReadOnlyDictionary<MapId,PretWorldMap> maps, ImmutableDictionary<MapId,RoamerMap> predecessor,
        IReadOnlyDictionary<MapId,PretWorldMap> additions)
    {
        if (predecessor.Count != 257 || additions.Count != 18 ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) )
            throw new InvalidDataException("seven_island_map_numbers_scope");
        var current = ProjectSource(root, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value))
            throw new InvalidDataException("seven_island_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendEventIslands(string root,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, RoamerMap> predecessor,
        IReadOnlyDictionary<MapId, PretWorldMap> additions)
    {
        if (predecessor.Count != 275 || additions.Count != 24 ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) ) throw new InvalidDataException("event_island_map_numbers_scope");
        var current = ProjectSource(root, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value)) throw new InvalidDataException("event_island_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId,RoamerMap> ExtendPowerPlant(string root,
        IReadOnlyDictionary<MapId,PretWorldMap> maps, ImmutableDictionary<MapId,RoamerMap> predecessor,
        IReadOnlyDictionary<MapId,PretWorldMap> additions)
    {
        if(!additions.Keys.ToHashSet().SetEquals(new[]{new MapId("map.power-plant")}) ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) ||
            predecessor.Keys.Any(id=>!maps.ContainsKey(id)) )
            throw new InvalidDataException("power_plant_map_numbers_scope");
        var current=ProjectSource(root,maps);
        if(predecessor.Any(p=>current[p.Key]!=p.Value))throw new InvalidDataException("power_plant_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p=>!predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendTrainerTower(string root,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, RoamerMap> predecessor,
        IReadOnlyDictionary<MapId, PretWorldMap> additions)
    {
        if (predecessor.Count != 300 || additions.Count != 11 ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) ||
            predecessor.Keys.Any(id => !maps.ContainsKey(id)) ||
            additions.Values.Any(m => !m.SourceMapSymbol.StartsWith("MAP_TRAINER_TOWER_", StringComparison.Ordinal)))
            throw new InvalidDataException("trainer_tower_map_numbers_scope");
        var current = ProjectSource(root, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value)) throw new InvalidDataException("trainer_tower_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendFightingDojo(string root,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, RoamerMap> predecessor,
        IReadOnlyDictionary<MapId, PretWorldMap> additions)
    {
        if (predecessor.Count != 311 || additions.Count != 1 ||
            !additions.ContainsKey(new("map.saffron-city-dojo")) ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) ||
            predecessor.Keys.Any(id => !maps.ContainsKey(id)) )
            throw new InvalidDataException("fighting_dojo_map_numbers_scope");
        var current = ProjectSource(root, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value)) throw new InvalidDataException("fighting_dojo_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendFossilLab(string root,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, RoamerMap> predecessor,
        IReadOnlyDictionary<MapId, PretWorldMap> additions)
    {
        if (predecessor.Count != 312 || additions.Count != 2 ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) ||
            predecessor.Keys.Any(id => !maps.ContainsKey(id)) ||
            additions.Values.Any(m => !m.SourceMapSymbol.StartsWith("MAP_CINNABAR_ISLAND_POKEMON_LAB_", StringComparison.Ordinal)))
            throw new InvalidDataException("fossil_lab_map_numbers_scope");
        var current = ProjectSource(root, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value)) throw new InvalidDataException("fossil_lab_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendAuthenticatedMaps(string root,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, RoamerMap> predecessor,
        IReadOnlyDictionary<MapId, PretWorldMap> additions)
    {
        if (maps.Count != predecessor.Count + additions.Count ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) )
            throw new InvalidDataException("current_map_numbers_additions");
        var current = ProjectSource(root, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value)) throw new InvalidDataException("current_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    private static ImmutableDictionary<MapId, RoamerMap> ProjectSource(string root, IReadOnlyDictionary<MapId, PretWorldMap> maps)
    {
        var source = LoadSourceTable(root);
        var result = ImmutableDictionary.CreateBuilder<MapId, RoamerMap>();
        foreach (var (id, map) in maps)
        {
            var path = map.SourceRef.Path.Split('/');
            if (map.Id != id || map.SourceRef.Kind != SourceKind.Pret || path.Length != 4 ||
                path[0] != "data" || path[1] != "maps" || path[3] != "map.json" ||
                !source.TryGetValue(path[2], out var number))
                throw new InvalidDataException("celio_map_numbers_unowned_map:" + id.Value);
            result.Add(id, number);
        }
        if (result.Values.Distinct().Count() != maps.Count) throw new InvalidDataException("celio_map_numbers_alias");
        return result.ToImmutable();
    }
}
