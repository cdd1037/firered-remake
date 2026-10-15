using System.Collections.Concurrent;
using System.Text.Json;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Shared converted tile behavior tables, including switch tiles absent from initial grids.</summary>
internal sealed class GeneratedFieldTilesets
{
    private sealed record Tables(Dictionary<string, string[]> Layouts, Dictionary<string, int[][]> Tilesets);
    private static readonly ConcurrentDictionary<string, Lazy<GeneratedFieldTilesets>> Cache = new(StringComparer.Ordinal);
    private readonly Tables _tables;
    private GeneratedFieldTilesets(string file)
    {
        _tables = JsonSerializer.Deserialize<Tables>(File.ReadAllBytes(file), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Missing generated field tile definitions.");
    }
    internal static GeneratedFieldTilesets ForPackage(string directory)
    {
        string file = Path.Combine(PretRuntimeMapValidation.GeneratedRoot(directory), "field-tilesets.json");
        return Cache.GetOrAdd(file, path => new(() => new GeneratedFieldTilesets(path))).Value;
    }
    internal FieldMetatileDefinition? Find(string layout, int metatile)
    {
        if (metatile is < 0 or > 1023 || !_tables.Layouts.TryGetValue(layout, out var pair)) return null;
        var rows = _tables.Tilesets[pair[metatile < 640 ? 0 : 1]];
        int index = metatile < 640 ? metatile : metatile - 640;
        if (index >= rows.Length) return null;
        var row = rows[index];
        return new(metatile, row[0], row[1], row[2], row[3]);
    }
}
