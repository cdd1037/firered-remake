using System.Collections.Immutable;
using System.Text;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>The current world's single map index, with named layout variants kept outside the playable map set.</summary>
public sealed class PretCurrentWorld
{
    public const string IndexPath = "content/generated/maps/world.json";
    private readonly string _projectRoot;
    private readonly ImmutableDictionary<string, PretWorldMap> _mapsByPath;
    public PretRuntimeMapCatalog RuntimeMaps { get; }
    public ImmutableDictionary<MapId, PretWorldMap> Maps { get; }

    private PretCurrentWorld(string projectRoot)
    {
        _projectRoot = Path.GetFullPath(projectRoot);
        RuntimeMaps = PretRuntimeMapCatalog.Load(Path.Combine(_projectRoot, IndexPath));
        var generated = Path.Combine(_projectRoot, "content/generated");
        _mapsByPath = RuntimeMaps.Entries.ToImmutableDictionary(e => e.Path,
            e => PretWorldMap.FromRuntime(RuntimeMaps.MapsByPath[e.Path],
                PretRuntimeMapValidation.SafeFile(generated, e.Path),
                PretRuntimeMapValidation.SafeFile(generated, e.PlayerTexturePath ?? "sprites/overworld/red-normal.png"),
                RuntimeMaps.Resolve, e.UsesLocalBorder), StringComparer.Ordinal);
        Maps = RuntimeMaps.Entries.Where(e => e.Variant is null)
            .ToImmutableDictionary(e => new MapId(e.StableId), e => _mapsByPath[e.Path]);
    }

    public string ProjectRoot => _projectRoot;
    public static PretCurrentWorld Load(string projectRoot) => new(projectRoot);

    public ImmutableDictionary<MapId, PretWorldMap> MapsInDirectory(string directory)
    {
        var prefix = Path.GetRelativePath(Path.Combine(_projectRoot, "content/generated"), Path.GetFullPath(directory))
            .Replace(Path.DirectorySeparatorChar, '/') + "/";
        var entries = RuntimeMaps.Entries.Where(e => e.Variant is null && e.Path.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        if (entries.Length == 0) throw new InvalidDataException("No current maps in directory: " + directory);
        return entries.ToImmutableDictionary(e => new MapId(e.StableId), e => _mapsByPath[e.Path]);
    }

    public ImmutableDictionary<string, PretWorldMap> VariantsInDirectory(string directory)
    {
        var prefix = Path.GetRelativePath(Path.Combine(_projectRoot, "content/generated"), Path.GetFullPath(directory))
            .Replace(Path.DirectorySeparatorChar, '/') + "/";
        return RuntimeMaps.Entries.Where(e => e.Variant is not null && e.Path.StartsWith(prefix, StringComparison.Ordinal))
            .ToImmutableDictionary(e => e.Layout ?? throw new InvalidDataException("Missing world layout variant symbol."),
                e => _mapsByPath[e.Path], StringComparer.Ordinal);
    }

    public MapId Resolve(string symbol) => RuntimeMaps.Resolve(symbol);
    public PretWorldMap GetMapByPath(string relative)
    {
        if (relative.StartsWith("content/generated/", StringComparison.Ordinal)) relative = relative["content/generated/".Length..];
        return _mapsByPath.TryGetValue(relative, out var map) ? map
            : throw new InvalidDataException("Map is not part of the current world: " + relative);
    }
    public byte[] ReadEvents(string sourcePath)
    {
        const string prefix = "data/maps/";
        const string suffix = "/map.json";
        if (!sourcePath.StartsWith(prefix, StringComparison.Ordinal) || !sourcePath.EndsWith(suffix, StringComparison.Ordinal))
            throw new InvalidDataException("Invalid map event identity: " + sourcePath);
        var name = sourcePath[prefix.Length..^suffix.Length];
        var map = Maps.Values.SingleOrDefault(m => m.SourceMapName == name)
            ?? throw new InvalidDataException("No current world map owns events: " + sourcePath);
        return Encoding.UTF8.GetBytes(map.Events.GetRawText());
    }
}
