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

    internal static ImmutableDictionary<MapId, RoamerMap> ExtendAuthenticatedMaps(ImmutableDictionary<string, RoamerMap> source,
        IReadOnlyDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<MapId, RoamerMap> predecessor,
        IReadOnlyDictionary<MapId, PretWorldMap> additions)
    {
        if (predecessor.Keys.Any(id => !maps.ContainsKey(id)) ||
            maps.Count != predecessor.Count + additions.Count ||
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
