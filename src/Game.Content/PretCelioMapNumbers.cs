using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Roaming;

namespace Game.Content;

/// <summary>Original map-group/member indexes, including distinct interior groups, for player history.</summary>
public static class PretCelioMapNumbers
{
    public const string AssetPath = "content/generated/maps/numbers.json";

    public static ImmutableDictionary<string, RoamerMap> LoadSourceTable(string root)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, AssetPath));
        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.EnumerateObject().ToImmutableDictionary(
            p => p.Name, p => new RoamerMap(p.Value[0].GetInt32(), p.Value[1].GetInt32()), StringComparer.Ordinal);
    }

    public static ImmutableDictionary<MapId, RoamerMap> Load(string root, IReadOnlyDictionary<MapId, PretWorldMap> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        return Load(LoadSourceTable(root), maps);
    }

    internal static ImmutableDictionary<MapId, RoamerMap> Load(ImmutableDictionary<string, RoamerMap> source,
        IReadOnlyDictionary<MapId, PretWorldMap> maps)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(maps);
        return ProjectSource(source, maps);
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendCeruleanCave(ImmutableDictionary<string, RoamerMap> source,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, RoamerMap> predecessor)
    {
        ArgumentNullException.ThrowIfNull(maps); ArgumentNullException.ThrowIfNull(predecessor);
        if (predecessor.Count != 254 || predecessor.Keys.Any(id => !maps.ContainsKey(id)) ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(new[] {
                new MapId("map.cerulean-cave-1f"),new MapId("map.cerulean-cave-2f"),new MapId("map.cerulean-cave-b1f") }))
            throw new InvalidDataException("cerulean_cave_map_numbers_scope");
        var current = ProjectSource(source, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value))
            throw new InvalidDataException("cerulean_cave_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendSevenIsland(ImmutableDictionary<string, RoamerMap> source,
        IReadOnlyDictionary<MapId,PretWorldMap> maps, ImmutableDictionary<MapId,RoamerMap> predecessor,
        IReadOnlyDictionary<MapId,PretWorldMap> additions)
    {
        if (predecessor.Count != 257 || additions.Count != 18 ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) )
            throw new InvalidDataException("seven_island_map_numbers_scope");
        var current = ProjectSource(source, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value))
            throw new InvalidDataException("seven_island_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendEventIslands(ImmutableDictionary<string, RoamerMap> source,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, RoamerMap> predecessor,
        IReadOnlyDictionary<MapId, PretWorldMap> additions)
    {
        if (predecessor.Count != 275 || additions.Count != 24 ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) ) throw new InvalidDataException("event_island_map_numbers_scope");
        var current = ProjectSource(source, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value)) throw new InvalidDataException("event_island_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId,RoamerMap> ExtendPowerPlant(ImmutableDictionary<string, RoamerMap> source,
        IReadOnlyDictionary<MapId,PretWorldMap> maps, ImmutableDictionary<MapId,RoamerMap> predecessor,
        IReadOnlyDictionary<MapId,PretWorldMap> additions)
    {
        if(!additions.Keys.ToHashSet().SetEquals(new[]{new MapId("map.power-plant")}) ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) ||
            predecessor.Keys.Any(id=>!maps.ContainsKey(id)) )
            throw new InvalidDataException("power_plant_map_numbers_scope");
        var current=ProjectSource(source,maps);
        if(predecessor.Any(p=>current[p.Key]!=p.Value))throw new InvalidDataException("power_plant_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p=>!predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendTrainerTower(ImmutableDictionary<string, RoamerMap> source,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, RoamerMap> predecessor,
        IReadOnlyDictionary<MapId, PretWorldMap> additions)
    {
        if (predecessor.Count != 300 || additions.Count != 11 ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) ||
            predecessor.Keys.Any(id => !maps.ContainsKey(id)) ||
            additions.Values.Any(m => !m.SourceMapSymbol.StartsWith("MAP_TRAINER_TOWER_", StringComparison.Ordinal)))
            throw new InvalidDataException("trainer_tower_map_numbers_scope");
        var current = ProjectSource(source, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value)) throw new InvalidDataException("trainer_tower_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendFightingDojo(ImmutableDictionary<string, RoamerMap> source,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, RoamerMap> predecessor,
        IReadOnlyDictionary<MapId, PretWorldMap> additions)
    {
        if (predecessor.Count != 311 || additions.Count != 1 ||
            !additions.ContainsKey(new("map.saffron-city-dojo")) ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) ||
            predecessor.Keys.Any(id => !maps.ContainsKey(id)) )
            throw new InvalidDataException("fighting_dojo_map_numbers_scope");
        var current = ProjectSource(source, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value)) throw new InvalidDataException("fighting_dojo_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendFossilLab(ImmutableDictionary<string, RoamerMap> source,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, RoamerMap> predecessor,
        IReadOnlyDictionary<MapId, PretWorldMap> additions)
    {
        if (predecessor.Count != 312 || additions.Count != 2 ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) ||
            predecessor.Keys.Any(id => !maps.ContainsKey(id)) ||
            additions.Values.Any(m => !m.SourceMapSymbol.StartsWith("MAP_CINNABAR_ISLAND_POKEMON_LAB_", StringComparison.Ordinal)))
            throw new InvalidDataException("fossil_lab_map_numbers_scope");
        var current = ProjectSource(source, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value)) throw new InvalidDataException("fossil_lab_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendAuthenticatedMaps(ImmutableDictionary<string, RoamerMap> source,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, RoamerMap> predecessor,
        IReadOnlyDictionary<MapId, PretWorldMap> additions)
    {
        if (maps.Count != predecessor.Count + additions.Count ||
            !maps.Keys.Except(predecessor.Keys).ToHashSet().SetEquals(additions.Keys) )
            throw new InvalidDataException("current_map_numbers_additions");
        var current = ProjectSource(source, maps);
        if (predecessor.Any(p => current[p.Key] != p.Value)) throw new InvalidDataException("current_map_numbers_predecessor");
        return predecessor.AddRange(current.Where(p => !predecessor.ContainsKey(p.Key)));
    }

    private static ImmutableDictionary<MapId, RoamerMap> ProjectSource(ImmutableDictionary<string, RoamerMap> source, IReadOnlyDictionary<MapId, PretWorldMap> maps)
    {
        var result = ImmutableDictionary.CreateBuilder<MapId, RoamerMap>();
        foreach (var (id, map) in maps)
        {
            if (map.Id != id || !source.TryGetValue(map.SourceMapName, out var number))
                throw new InvalidDataException("celio_map_numbers_unowned_map:" + id.Value);
            result.Add(id, number);
        }
        if (result.Values.Distinct().Count() != maps.Count) throw new InvalidDataException("celio_map_numbers_alias");
        return result.ToImmutable();
    }
}
