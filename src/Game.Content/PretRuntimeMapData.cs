using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed record PretRuntimeMapBehavior(int Id, string Name);
public sealed record PretRuntimeMapEntry(string StableId, string Path, string SourceMapSymbol, string? Variant = null, string? PlayerTexturePath = null, string? Layout = null, bool UsesLocalBorder = true);
internal sealed record PretRuntimeMapManifest(int SchemaVersion, ImmutableArray<PretRuntimeMapEntry> Maps,
    ImmutableDictionary<string, string> MapSymbols);
internal sealed record PretRuntimeMapDocument(int SchemaVersion, string StableId, string SourceMapSymbol, string SourceMapName,
    int Width, int Height, int TileSize, int BorderWidth, int BorderHeight, string TexturePath,
    ImmutableArray<PretWorldArtifact> Artifacts, PretWorldSpawn? TechnicalSpawn, JsonElement Events,
    string Blocks, string BorderBlocks,
    ImmutableArray<ImmutableArray<uint>> Metatiles, ImmutableArray<PretRuntimeMapBehavior> Behaviors,
    ImmutableArray<string> ScriptPaths, MapPreviewDefinition? MapPreview);

/// <summary>
/// Map catalogs reference the one compact payload at each ordinary generated path.
/// </summary>
public sealed class PretRuntimeMapCatalog
{
    public ImmutableDictionary<MapId, PretRuntimeMapData> Maps { get; }
    public ImmutableArray<PretRuntimeMapEntry> Entries { get; }
    public ImmutableDictionary<string, PretRuntimeMapData> MapsByPath { get; }
    private readonly ImmutableDictionary<string, MapId> _mapIds;
    public MapId Resolve(string symbol) => symbol == "MAP_DYNAMIC" ? PretRocketHideoutRegion.DynamicMapId
        : _mapIds.TryGetValue(symbol, out var id) ? id
        : throw new InvalidDataException("Unknown world map symbol: " + symbol);

    private PretRuntimeMapCatalog(ImmutableDictionary<MapId, PretRuntimeMapData> maps,
        ImmutableDictionary<string, PretRuntimeMapData> mapsByPath, ImmutableArray<PretRuntimeMapEntry> entries,
        ImmutableDictionary<string, MapId> mapIds)
        => (Maps, MapsByPath, Entries, _mapIds) = (maps, mapsByPath, entries, mapIds);

    public static PretRuntimeMapCatalog Load(string path)
    {
        PretRuntimeMapValidation.RequireNoLinks(Path.GetDirectoryName(Path.GetFullPath(path))!, Path.GetFileName(path));
        var manifest = PretRuntimeMapValidation.Decode<PretRuntimeMapManifest>(File.ReadAllBytes(path));
        PretRuntimeMapValidation.Require(manifest.SchemaVersion == 3 && !manifest.Maps.IsDefaultOrEmpty,
            "Unsupported current world format.");
        PretRuntimeMapValidation.Require(manifest.MapSymbols is { Count: > 0 },
            "Missing current world symbol registry.");
        foreach (var (symbol, stableId) in manifest.MapSymbols!)
            PretRuntimeMapValidation.Require(symbol.StartsWith("MAP_", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(stableId), "Invalid current world symbol identity.");
        var mapIds = manifest.MapSymbols.ToImmutableDictionary(p => p.Key, p => new MapId(p.Value), StringComparer.Ordinal);
        MapId Resolve(string symbol) => symbol == "MAP_DYNAMIC" ? PretRocketHideoutRegion.DynamicMapId
            : mapIds.TryGetValue(symbol, out var id) ? id
            : throw new InvalidDataException("Unknown world map symbol: " + symbol);
        var ordinary = manifest.Maps.Where(e => e.Variant is null).ToArray();
        PretRuntimeMapValidation.Require(ordinary.Length > 0 && ordinary.Select(e => e.StableId).Distinct().Count() == ordinary.Length &&
            ordinary.Select(e => e.SourceMapSymbol).Distinct().Count() == ordinary.Length,
            "Duplicate ordinary world map identity.");
        var ordinaryIds = ordinary.Select(e => e.StableId).ToHashSet(StringComparer.Ordinal);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretRuntimeMapData>();
        var byPath = ImmutableDictionary.CreateBuilder<string, PretRuntimeMapData>(StringComparer.Ordinal);
        var variants = new HashSet<(string, string)>();
        foreach (var entry in manifest.Maps)
        {
            PretRuntimeMapValidation.Require(mapIds.TryGetValue(entry.SourceMapSymbol, out var id) && id.Value == entry.StableId,
                "World index symbol identity differs.");
            if (entry.Variant is not null)
                PretRuntimeMapValidation.Require(!string.IsNullOrWhiteSpace(entry.Variant) && ordinaryIds.Contains(entry.StableId) &&
                    variants.Add((entry.StableId, entry.Variant)), "Invalid or duplicate world layout variant.");
            var file = PretRuntimeMapValidation.SafeFile(PretRuntimeMapValidation.GeneratedRoot(path), entry.Path);
            PretRuntimeMapValidation.RequireNoLinks(PretRuntimeMapValidation.GeneratedRoot(path), entry.Path);
            var map = PretRuntimeMapData.Decode(File.ReadAllBytes(file), entry.StableId, Resolve);
            PretRuntimeMapValidation.Require(map.SourceMapSymbol == entry.SourceMapSymbol,
                "World payload source symbol differs from index.");
            PretRuntimeMapValidation.Require(byPath.TryAdd(entry.Path, map), "Duplicate world map path.");
            if (entry.Variant is null) maps.Add(map.Id, map);
        }
        foreach (var map in byPath.Values)
        {
            foreach (var warp in map.FieldWarps)
                if (maps.TryGetValue(warp.DestinationMapId, out var target) && warp.DestinationWarpId >= 0)
                    PretRuntimeMapValidation.Require(warp.DestinationWarpId < target.FieldWarps.Length,
                        "Runtime warp endpoint is outside its destination.");
            if (map.Events.GetProperty("connections") is { ValueKind: JsonValueKind.Array } connections)
                foreach (var connection in connections.EnumerateArray()) _ = Resolve(connection.GetProperty("map").GetString()!);
        }
        return new(maps.ToImmutable(), byPath.ToImmutable(), manifest.Maps, mapIds);
    }

}

/// <summary>
/// Compact cells use original 16-bit block values and a per-metatile attribute table.
/// Source cells and metatile references expand only when a caller requests them.
/// </summary>
public sealed class PretRuntimeMapData : IFieldMap
{
    private readonly PretRuntimeMapDocument _data;
    private readonly ImmutableArray<ushort> _blocks;
    private readonly ImmutableArray<ushort> _borderBlocks;
    private readonly ImmutableDictionary<int, uint> _metatiles;
    private readonly ImmutableDictionary<int, string> _behaviors;
    private readonly Func<string, MapId> _mapIdFromSymbol;
    public MapId Id { get; }
    internal MapId ResolveMapSymbol(string symbol) => _mapIdFromSymbol(symbol);
    public int Width => _data.Width;
    public int Height => _data.Height;
    public int TileSize => _data.TileSize;
    public int BorderWidth => _data.BorderWidth;
    public int BorderHeight => _data.BorderHeight;
    public string SourceMapSymbol => _data.SourceMapSymbol;
    public string SourceMapName => _data.SourceMapName;
    public string TexturePath => _data.TexturePath;
    public JsonElement Events => _data.Events;
    public MapPreviewDefinition? MapPreview => _data.MapPreview;
    public PretWorldSpawn? TechnicalSpawn => _data.TechnicalSpawn;
    public ImmutableArray<PretWorldArtifact> Artifacts => _data.Artifacts;
    public ImmutableArray<string> ScriptPaths => _data.ScriptPaths;
    public ImmutableArray<ushort> Blocks => _blocks;
    public ImmutableArray<ushort> BorderBlocks => _borderBlocks;
    public ImmutableArray<FieldWarp> FieldWarps => Events.GetProperty("warp_events").EnumerateArray().Select((e, index) =>
        new FieldWarp(index, e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(),
            _mapIdFromSymbol(e.GetProperty("dest_map").GetString()!),
            PretRocketHideoutRegion.ParseWarpId(SourceMapSymbol, e))).ToImmutableArray();

    private PretRuntimeMapData(PretRuntimeMapDocument data, ImmutableArray<ushort> blocks,
        ImmutableArray<ushort> borderBlocks, ImmutableDictionary<int, uint> metatiles,
        ImmutableDictionary<int, string> behaviors, Func<string, MapId>? mapResolver = null)
    {
        _data = data; _blocks = blocks; _borderBlocks = borderBlocks; _metatiles = metatiles; _behaviors = behaviors;
        Id = new(data.StableId);
        _mapIdFromSymbol = mapResolver ?? (symbol => throw new InvalidDataException("A world symbol resolver is required: " + symbol));
    }

    public TileCell? GetTile(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return null;
        var block = PretWorldDecoder.DecodeBlock(_blocks[y * Width + x]);
        return new(block.Collision != 0, block.Elevation,
            PretWorldDecoder.Behavior((int)(_metatiles[block.MetatileId] & 511)));
    }

    public int? SourceBehaviorAt(int x, int y)
        => x < 0 || y < 0 || x >= Width || y >= Height ? null : (int)(_metatiles[_blocks[y * Width + x] & 1023] & 511);

    public PretWorldCell? GetSourceCell(int x, int y)
        => x < 0 || y < 0 || x >= Width || y >= Height ? null : Expand(_blocks[y * Width + x], x, y);

    public ImmutableArray<PretWorldCell> ExpandCells()
        => _blocks.Select((block, i) => Expand(block, i % Width, i / Width)).ToImmutableArray();

    internal ImmutableArray<ConnectedFieldCell> CreateConnectedCells()
    {
        var cells = ImmutableArray.CreateBuilder<ConnectedFieldCell>(_blocks.Length);
        foreach (var block in _blocks)
            cells.Add(new((int)(_metatiles[block & 1023] & 511), (block >> 10) & 3, block >> 12));
        return cells.MoveToImmutable();
    }

    public ImmutableArray<PretWorldCell> ExpandBorderCells()
        => _borderBlocks.Select((block, i) => Expand(block, i % BorderWidth, i / BorderWidth)).ToImmutableArray();

    private PretWorldCell Expand(ushort raw, int x, int y)
    {
        var block = PretWorldDecoder.DecodeBlock(raw);
        var metatile = _metatiles[block.MetatileId];
        var a = PretWorldDecoder.DecodeAttributes(metatile);
        return new(x, y, raw, block.MetatileId, block.Collision, block.Elevation, metatile,
            a.BehaviorId, _behaviors[a.BehaviorId], a.Terrain, a.Attribute2, a.Attribute3, a.EncounterType,
            a.Attribute5, a.LayerType, a.Attribute7, PretWorldDecoder.Movement(a.BehaviorId));
    }

    internal static PretRuntimeMapData Decode(byte[] bytes, PretRuntimeMapEntry expected) => Decode(bytes, expected.StableId);

    internal static PretRuntimeMapData Decode(byte[] bytes, string expectedStableId, Func<string, MapId>? mapResolver = null)
    {
        try { return DecodeCore(bytes, expectedStableId, mapResolver); }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or ArgumentException or OverflowException)
        { throw new InvalidDataException("Invalid runtime map structure.", error); }
    }

    private static PretRuntimeMapData DecodeCore(byte[] bytes, string expectedStableId, Func<string, MapId>? mapResolver)
    {
        var d = PretRuntimeMapValidation.Decode<PretRuntimeMapDocument>(bytes);
        void Require(bool condition, string message) => PretRuntimeMapValidation.Require(condition, message);
        Require(d.SchemaVersion == 3 &&
            d.StableId == expectedStableId,
            "Runtime map version/source identity differs.");
        Require(d.MapPreview is null || (Enum.IsDefined(d.MapPreview.Type) &&
            (d.MapPreview.VisitFlag is null ? d.MapPreview.Type == MapPreviewType.Forest :
             d.MapPreview.VisitFlag.StartsWith("FLAG_", StringComparison.Ordinal) && d.MapPreview.VisitFlag.Length is > 5 and <= 128 &&
             d.MapPreview.VisitFlag.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_'))),
            "Invalid runtime map preview metadata.");
        Require(d.Width is > 0 and <= 1024 && d.Height is > 0 and <= 1024 && (long)d.Width * d.Height <= 262144 &&
            d.TileSize == 16 && d.BorderWidth is > 0 and <= 64 && d.BorderHeight is > 0 and <= 64,
            "Runtime map dimensions out of bounds.");
        Require(!string.IsNullOrWhiteSpace(d.SourceMapSymbol) && d.SourceMapSymbol.StartsWith("MAP_", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(d.SourceMapName) && d.SourceMapName.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'),
            "Invalid runtime map identity.");
        Require(!d.Metatiles.IsDefaultOrEmpty && d.Metatiles.Length <= 1024 && !d.Behaviors.IsDefaultOrEmpty && d.Behaviors.Length <= 512,
            "Incomplete runtime map tables.");
        var behaviors = ImmutableDictionary.CreateBuilder<int, string>();
        foreach (var behavior in d.Behaviors)
            Require(behavior is not null && behavior.Id is >= 0 and <= 511 &&
                !string.IsNullOrWhiteSpace(behavior.Name) && behavior.Name.Length <= 100 &&
                behaviors.TryAdd(behavior.Id, behavior.Name), "Invalid/duplicate runtime behavior.");
        var metatiles = ImmutableDictionary.CreateBuilder<int, uint>();
        var usedBehaviors = new HashSet<int>();
        foreach (var entry in d.Metatiles)
        {
            Require(!entry.IsDefault && entry.Length == 2 && entry[0] <= 1023,
                "Runtime metatile index out of bounds.");
            var id = (int)entry[0];
            var decoded = PretWorldDecoder.DecodeAttributes(entry[1]);
            Require(decoded.LayerType <= 2 && behaviors.ContainsKey(decoded.BehaviorId), "Runtime attribute layer/behavior is invalid.");
            Require(metatiles.TryAdd(id, entry[1]), "Duplicate runtime metatile ID.");
            usedBehaviors.Add(decoded.BehaviorId);
        }
        Require(usedBehaviors.SetEquals(behaviors.Keys), "Unused runtime behavior table entry.");
        ImmutableArray<ushort> DecodeBlocks(string base64, int count)
        {
            Require(base64 is not null && base64.Length == ((count * 2 + 2) / 3) * 4, "Runtime block encoding length differs.");
            byte[] payload;
            try { payload = Convert.FromBase64String(base64!); }
            catch (FormatException error) { throw new InvalidDataException("Invalid runtime block encoding.", error); }
            Require(payload.Length == count * 2 && Convert.ToBase64String(payload) == base64, "Runtime blocks are incomplete/noncanonical.");
            var blocks = ImmutableArray.CreateBuilder<ushort>(count);
            for (var i = 0; i < count; i++)
            {
                var block = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(i * 2, 2));
                Require(metatiles.ContainsKey(block & 1023), "Runtime block refers to a missing metatile.");
                blocks.Add(block);
            }
            return blocks.MoveToImmutable();
        }
        var cells = DecodeBlocks(d.Blocks, d.Width * d.Height);
        var border = DecodeBlocks(d.BorderBlocks, d.BorderWidth * d.BorderHeight);
        Require(cells.Concat(border).Select(v => (int)(v & 1023)).ToHashSet().SetEquals(metatiles.Keys), "Unused runtime metatile definition.");
        ValidateEvents(d.Events, d.SourceMapSymbol, d.Width, d.Height);
        if (d.TechnicalSpawn is { } spawn)
        {
            Require(spawn.X >= 0 && spawn.X < d.Width && spawn.Y >= 0 && spawn.Y < d.Height && spawn.Elevation is >= 0 and <= 15 &&
                !string.IsNullOrWhiteSpace(spawn.Reason), "Runtime spawn out of bounds.");
            var block = PretWorldDecoder.DecodeBlock(cells[spawn.Y * d.Width + spawn.X]);
            Require(block.Collision == 0 && block.Elevation == spawn.Elevation && (metatiles[block.MetatileId] & 511) == 0,
                "Runtime spawn is not an ordinary walkable source cell.");
        }
        Require(!d.ScriptPaths.IsDefaultOrEmpty && d.ScriptPaths.Length <= 2, "Missing runtime map scripts.");
        var scriptPaths = new HashSet<string>(StringComparer.Ordinal);
        var stem = "data/maps/" + d.SourceMapName + "/";
        foreach (var path in d.ScriptPaths)
            Require(path is not null && (path == stem + "scripts.inc" || path == stem + "text.inc") &&
                scriptPaths.Add(path), "Invalid runtime script path.");
        Require(scriptPaths.Contains(stem + "scripts.inc"), "Missing live runtime script dependency.");
        Require(Path.GetFileName(d.TexturePath) == d.TexturePath && d.TexturePath.EndsWith("map.png", StringComparison.Ordinal) && !d.Artifacts.IsDefaultOrEmpty && d.Artifacts.Length <= 9, "Invalid runtime artifact inventory.");
        var artifacts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var artifact in d.Artifacts)
        {
            Require(artifact is not null && artifacts.Add(artifact.Path), "Invalid/duplicate runtime artifact.");
            var a = artifact!;
            var artifactName = a.Path.StartsWith(d.TexturePath[..^"map.png".Length], StringComparison.Ordinal) ? a.Path[d.TexturePath[..^"map.png".Length].Length..] : a.Path;
            Require(Path.GetFileName(a.Path) == a.Path && artifactName is "bottom.png" or "middle.png" or "top.png" or "map.png" or
                "border.png" or "border-bottom.png" or "border-middle.png" or "border-top.png", "Unknown runtime artifact path.");
            var size = artifactName.StartsWith("border", StringComparison.Ordinal) ?
                (d.BorderWidth * 16, d.BorderHeight * 16) : (d.Width * 16, d.Height * 16);
            Require((a.Width, a.Height) == size, "Runtime artifact dimensions differ.");
        }
        Require(new[] { "bottom.png", "middle.png", "top.png", "map.png" }.All(name => artifacts.Contains(d.TexturePath[..^"map.png".Length] + name)), "Missing runtime visual layer.");
        return new(d, cells, border, metatiles.ToImmutable(), behaviors.ToImmutable(), mapResolver);
    }

    private static void ValidateEvents(JsonElement events, string symbol, int width, int height)
    {
        void Require(bool condition, string message) => PretRuntimeMapValidation.Require(condition, message);
        Require(events.ValueKind == JsonValueKind.Object && events.GetProperty("id").GetString() == symbol, "Runtime event identity differs.");
        foreach (var name in new[] { "object_events", "warp_events", "coord_events", "bg_events" })
        {
            var entries = events.GetProperty(name);
            Require(entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() <= 4096, "Invalid runtime event array.");
            foreach (var e in entries.EnumerateArray())
            {
                var x = e.GetProperty("x").GetInt32(); var y = e.GetProperty("y").GetInt32();
                // Original neighbor clones deliberately sit outside the source rectangle.
                var clone = name == "object_events" && e.TryGetProperty("type", out var type) && type.GetString() == "clone";
                Require(clone ? x is >= -1024 and <= 2048 && y is >= -1024 and <= 2048 : x >= 0 && y >= 0 && x < width && y < height,
                    "Runtime event coordinate out of bounds.");
                if (!clone) Require(e.GetProperty("elevation").GetInt32() is >= 0 and <= 15, "Runtime event elevation out of bounds.");
                if (name == "warp_events")
                {
                    Require(e.GetProperty("dest_map").GetString() is { Length: > 0 }, "Missing runtime warp destination.");
                    _ = PretRocketHideoutRegion.ParseWarpId(symbol, e);
                }
            }
        }
        var connections = events.GetProperty("connections");
        Require(connections.ValueKind is JsonValueKind.Null or JsonValueKind.Array, "Invalid runtime connections.");
        if (connections.ValueKind == JsonValueKind.Array)
        {
            Require(connections.GetArrayLength() <= 16, "Too many runtime connections.");
            foreach (var c in connections.EnumerateArray())
                Require(c.GetProperty("direction").GetString() is "up" or "down" or "left" or "right" &&
                    c.GetProperty("offset").GetInt32() is >= -1024 and <= 1024 && c.GetProperty("map").GetString() is { Length: > 0 },
                    "Invalid runtime connection direction/offset/destination.");
        }
    }
}

internal static class PretRuntimeMapValidation
{
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    internal static string ProjectRoot(string path) => Path.GetFullPath(Path.Combine(GeneratedRoot(path), "../.."));
    internal static string GeneratedRoot(string path)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(path)); directory is not null; directory = directory.Parent)
            if (directory.Name == "generated" && directory.Parent?.Name == "content") return directory.FullName;
        throw new InvalidDataException("Map path is outside content/generated.");
    }
    internal static string SafeFile(string root, string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\') &&
            !relative.Contains(':') && relative.Split('/').All(p => p is not ("" or "." or "..")), "Unsafe runtime map path.");
        return Path.Combine(Path.GetFullPath(root), relative);
    }
    internal static void RequireNoLinks(string directory, string relative)
    {
        var path = SafeFile(directory, relative);
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Runtime map symlink rejected.");
    }
    internal static T Decode<T>(byte[] bytes)
    {
        try
        {
            using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
            CheckDuplicates(doc.RootElement);
            return doc.RootElement.Deserialize<T>(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                RespectRequiredConstructorParameters = true,
                Converters = { new ExactEnumConverter<MapPreviewType>(), new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
            }) ?? throw new InvalidDataException("Null runtime map JSON.");
        }
        catch (JsonException error) { throw new InvalidDataException("Invalid runtime map JSON.", error); }
    }
    private static void CheckDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in element.EnumerateObject())
            {
                Require(properties.Add(p.Name), "Duplicate runtime JSON property: " + p.Name);
                CheckDuplicates(p.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) CheckDuplicates(child);
    }
}
