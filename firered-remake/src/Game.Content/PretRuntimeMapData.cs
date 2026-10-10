using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed record PretRuntimeMapSource(string Path, string Sha256);
public sealed record PretRuntimeMapBehavior(int Id, string Name);
public sealed record PretRuntimeMapScript(string Path, string Sha256, string Text);
public sealed record PretRuntimeMapEntry(string StableId, string Path, int ByteLength, string Sha256,
    string SourcePath, int SourceByteLength, string SourceWorldSha256, string FullSourceHash);
internal sealed record PretRuntimeMapRegistryEntry(string Path, int ByteLength, string Sha256);
internal sealed record PretRuntimeMapRegistry(int SchemaVersion, string ConverterVersion, string SourceCommit,
    PretWorldMetadata Metadata);
internal sealed record PretRuntimeMapManifest(int SchemaVersion, string ConverterVersion, string SourceCommit,
    string ConverterSha256, ImmutableArray<PretRuntimeMapEntry> Maps, PretRuntimeMapRegistryEntry SourceRegistry);
internal sealed record PretRuntimeMapDocument(int SchemaVersion, string ConverterVersion, string SourceCommit,
    string SourceWorldSha256, string FullSourceHash, string StableId, SourceRef SourceRef,
    int Width, int Height, int TileSize, int BorderWidth, int BorderHeight, string TexturePath,
    ImmutableArray<PretWorldArtifact> Artifacts, PretWorldSpawn? TechnicalSpawn, JsonElement Events,
    string Blocks, string BorderBlocks, ImmutableArray<PretRuntimeMapSource> AttributeSources,
    ImmutableArray<ImmutableArray<uint>> Metatiles, ImmutableArray<PretRuntimeMapBehavior> Behaviors,
    ImmutableArray<PretRuntimeMapScript> Scripts);

/// <summary>
/// Authenticated local runtime map package. Trust comes from the caller/compiled manifest digest,
/// never from a hash read beside the package. Rich world.json remains a development artifact.
/// </summary>
public sealed class PretRuntimeMapCatalog
{
    public const string ConverterVersion = "firered-runtime-map-v1";
    public const string PinnedConverterSha256 = "9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8";
    public const string Route3ConverterSha256 = "559ef404effcfb4a13cc81ac9dd9e66f3e234181abebc8da278ebd9161660477";
    public const string BillConverterSha256 = "32e0fdeac71c51314d245ad88f9ec0451ea1f0cd5dc726a5ccbb97b0100fc9aa";
    public const string BillManifestSha256 = "970f0c808a3f49c085ac11208bf581a40cf931daffa8dace3225b129d05653e0";
    public const string CeruleanConverterSha256 = "7eca0867416f7e051064bae1ab4457a9ab67dc2c52f0d3158bfd0c6793a35643";
    public const string CeruleanManifestSha256 = "26ccb6777e14b1afcbaf9e76611c93018331b77e968e2277651f2e185d915052";
    public const string MtMoonConverterSha256 = "bc1a2eb48ee7365f21bc75c3d9ec74cb2c1c971ae4a8bd1bee59b1b5f4727bcf";
    public const string MtMoonManifestSha256 = "b04995807d2db4d711c887c244f788ffd6905367819481f148ced64a4786fb9f";
    public const string Route3ManifestSha256 = "c023effe45bbf4c4155c9cfe5c46a081afe423afcbd393d6c12d1c29be9a830c";
    public const string PinnedManifestSha256 = "bd47464db904e8c9e49a0f7f4975d048bb9d8e04d43d931b6e5b096e9a82c2f7";
    public ImmutableDictionary<MapId, PretRuntimeMapData> Maps { get; }
    public ImmutableArray<PretRuntimeMapEntry> Entries { get; }
    public string ManifestSha256 { get; }
    public PretWorldMetadata BaseMetadata { get; }
    private PretRuntimeMapCatalog(ImmutableDictionary<MapId, PretRuntimeMapData> maps,
        ImmutableArray<PretRuntimeMapEntry> entries, string manifestSha256, PretWorldMetadata baseMetadata)
        => (Maps, Entries, ManifestSha256, BaseMetadata) = (maps, entries, manifestSha256, baseMetadata);

    public static PretRuntimeMapCatalog Load(string directory, string expectedManifestSha256 = PinnedManifestSha256)
    {
        var bytes = PretRuntimeMapValidation.ReadBounded(PretRuntimeMapValidation.SafeFile(directory, "manifest.json"), 256 * 1024);
        PretRuntimeMapValidation.Require(PretRuntimeMapValidation.IsHash(expectedManifestSha256) &&
            PretRuntimeMapValidation.Hash(bytes) == expectedManifestSha256, "Runtime map manifest hash differs.");
        var manifest = PretRuntimeMapValidation.Decode<PretRuntimeMapManifest>(bytes);
        bool route3 = expectedManifestSha256 == Route3ManifestSha256;
        bool mtMoon = expectedManifestSha256 == MtMoonManifestSha256;
        bool cerulean = expectedManifestSha256 == CeruleanManifestSha256;
        bool vermilion = expectedManifestSha256 == PretVermilionRegion.RuntimeManifestSha256;
        bool ssAnne = expectedManifestSha256 == PretSsAnneRuntimeMaps.ManifestSha256;
        bool vermilionGym = expectedManifestSha256 == PretVermilionGymRuntimeMaps.ManifestSha256;
        bool route11 = expectedManifestSha256 == PretRoute11RegionRuntimeMaps.ManifestSha256;
        bool cityServices = expectedManifestSha256 == PretCityServicesRuntimeMaps.ManifestSha256;
        bool bill = expectedManifestSha256 == BillManifestSha256;
        PretRuntimeMapValidation.Require(manifest.SchemaVersion == 1 && manifest.ConverterVersion == ConverterVersion &&
            manifest.ConverterSha256 == (route11 ? PretRoute11RegionRuntimeMaps.ConverterSha256 : cityServices ? PretCityServicesRuntimeMaps.ConverterSha256 : vermilionGym ? PretVermilionGymRuntimeMaps.ConverterSha256 : ssAnne ? PretSsAnneRuntimeMaps.ConverterSha256 : vermilion ? PretVermilionRegion.RuntimeConverterSha256 : bill ? BillConverterSha256 : cerulean ? CeruleanConverterSha256 : mtMoon ? MtMoonConverterSha256 : route3 ? Route3ConverterSha256 : PinnedConverterSha256) && manifest.SourceCommit == PretWorldMap.SourceCommit,
            "Runtime map manifest version/converter/source differs.");
        PretRuntimeMapValidation.Require(!manifest.Maps.IsDefault && manifest.Maps.Length == (route11 ? 91 : cityServices ? 83 : vermilionGym ? 81 : ssAnne ? 80 : vermilion ? 54 : bill ? 44 : cerulean ? 41 : mtMoon ? 30 : route3 ? 27 : 24),
            "Runtime map collection is incomplete.");
        if (ssAnne) PretSsAnneRuntimeMaps.ValidatePackage(directory, manifest.Maps);
        if (vermilionGym) PretVermilionGymRuntimeMaps.ValidatePackage(directory, manifest.Maps);
        if (cityServices) PretCityServicesRuntimeMaps.ValidatePackage(directory, manifest.Maps);
        if (route11) PretRoute11RegionRuntimeMaps.ValidatePackage(directory, manifest.Maps);
        var baseMetadata = LoadBaseMetadata(directory, manifest.SourceRegistry);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretRuntimeMapData>();
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var sourcePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in manifest.Maps)
        {
            PretRuntimeMapValidation.Require(entry is not null && !string.IsNullOrWhiteSpace(entry.StableId) &&
                entry.StableId.StartsWith("map.", StringComparison.Ordinal) &&
                entry.ByteLength is > 0 and <= PretRuntimeMapValidation.MaximumMapBytes && entry.SourceByteLength > 0 &&
                PretRuntimeMapValidation.IsHash(entry.Sha256) && PretRuntimeMapValidation.IsHash(entry.SourceWorldSha256) &&
                PretRuntimeMapValidation.IsHash(entry.FullSourceHash), "Invalid runtime map entry.");
            var current = entry!;
            var file = PretRuntimeMapValidation.SafeFile(directory, current.Path);
            _ = PretRuntimeMapValidation.SafeFile(directory, current.SourcePath);
            PretRuntimeMapValidation.Require(current.Path.EndsWith("/runtime-map.json", StringComparison.Ordinal) &&
                current.SourcePath == current.Path.Replace("/runtime-map.json", "/world.json", StringComparison.Ordinal) &&
                paths.Add(current.Path) && sourcePaths.Add(current.SourcePath), "Duplicate/invalid runtime map path.");
            var payload = PretRuntimeMapValidation.ReadBounded(file, PretRuntimeMapValidation.MaximumMapBytes);
            PretRuntimeMapValidation.Require(payload.Length == current.ByteLength && PretRuntimeMapValidation.Hash(payload) == current.Sha256,
                "Runtime map payload length/hash differs: " + current.Path);
            var map = PretRuntimeMapData.Decode(payload, current);
            PretRuntimeMapValidation.Require(maps.TryAdd(map.Id, map), "Duplicate runtime map ID.");
        }
        foreach (var map in maps.Values)
            foreach (var warp in map.FieldWarps)
                if (maps.TryGetValue(warp.DestinationMapId, out var target))
                    PretRuntimeMapValidation.Require(warp.DestinationWarpId < target.FieldWarps.Length,
                        "Runtime warp endpoint is outside its loaded destination inventory.");
        return new(maps.ToImmutable(), manifest.Maps, expectedManifestSha256, baseMetadata);
    }

    private static PretWorldMetadata LoadBaseMetadata(string directory, PretRuntimeMapRegistryEntry entry)
    {
        void Require(bool value, string message) => PretRuntimeMapValidation.Require(value, message);
        Require(entry is not null && entry.Path == "source-registry.json" && entry.ByteLength is > 0 and <= 256 * 1024 &&
            PretRuntimeMapValidation.IsHash(entry.Sha256), "Invalid base runtime source registry entry.");
        var bytes = PretRuntimeMapValidation.ReadBounded(Path.Combine(directory, entry!.Path), 256 * 1024);
        Require(bytes.Length == entry.ByteLength && PretRuntimeMapValidation.Hash(bytes) == entry.Sha256,
            "Base runtime source registry hash/length differs.");
        var registry = PretRuntimeMapValidation.Decode<PretRuntimeMapRegistry>(bytes);
        Require(registry.SchemaVersion == 1 && registry.ConverterVersion == ConverterVersion && registry.SourceCommit == PretWorldMap.SourceCommit,
            "Base runtime source registry version differs.");
        var metadata = registry.Metadata;
        Require(metadata is not null && metadata.Repository == "pret/pokefirered" && metadata.Commit == PretWorldMap.SourceCommit &&
            metadata.FullSourceHash == PretWorldMap.FullSourceHash && !metadata.Files.IsDefault && metadata.Files.Length == 128,
            "Base runtime source metadata identity/inventory differs.");
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in metadata!.Files)
        {
            Require(file is not null && file.ByteLength >= 0 && PretRuntimeMapValidation.IsHash(file.Sha256) &&
                file.GitBlobSha1 is { Length: 40 } && !string.IsNullOrWhiteSpace(file.SourceUrl), "Invalid base runtime source file.");
            _ = PretRuntimeMapValidation.SafeFile(".", file!.Path);
            Require(paths.Add(file.Path), "Duplicate base runtime source path.");
        }
        var ordered = metadata.Files.OrderBy(f => f.Path, StringComparer.Ordinal);
        var material = string.Concat(ordered.Select(f => f.Path + "\0" + f.Sha256 + "\n"));
        Require(PretRuntimeMapValidation.Hash(Encoding.UTF8.GetBytes(material)) == PretWorldMap.FullSourceHash,
            "Base runtime source set hash differs.");
        var inventory = string.Concat(ordered.Select(f => f.Path + "\0" + f.GitBlobSha1 + "\0" +
            f.ByteLength.ToString(CultureInfo.InvariantCulture) + "\0" + f.Sha256 + "\0" + f.SourceUrl + "\n"));
        Require(PretRuntimeMapValidation.Hash(Encoding.UTF8.GetBytes(inventory)) == PretWorldMap.SourceInventoryHash,
            "Base runtime pinned source inventory differs.");
        return metadata;
    }
}

/// <summary>
/// Compact cells use original 16-bit block values and a per-metatile attribute table.
/// Source cells expand only on request, keeping legacy public records byte-for-byte compatible.
/// </summary>
public sealed class PretRuntimeMapData : IFieldMap
{
    private readonly PretRuntimeMapDocument _data;
    private readonly ImmutableArray<ushort> _blocks;
    private readonly ImmutableArray<ushort> _borderBlocks;
    private readonly ImmutableDictionary<int, (uint Attributes, SourceRef Source)> _metatiles;
    private readonly ImmutableDictionary<int, string> _behaviors;
    private readonly Func<string, MapId> _mapIdFromSymbol;
    public MapId Id { get; }
    public int Width => _data.Width;
    public int Height => _data.Height;
    public int TileSize => _data.TileSize;
    public int BorderWidth => _data.BorderWidth;
    public int BorderHeight => _data.BorderHeight;
    public SourceRef SourceRef => _data.SourceRef;
    public string SourceWorldSha256 => _data.SourceWorldSha256;
    public string FullSourceHash => _data.FullSourceHash;
    public string TexturePath => _data.TexturePath;
    public JsonElement Events => _data.Events;
    public PretWorldSpawn? TechnicalSpawn => _data.TechnicalSpawn;
    public ImmutableArray<PretWorldArtifact> Artifacts => _data.Artifacts;
    public ImmutableArray<PretRuntimeMapScript> Scripts => _data.Scripts;
    public ImmutableArray<ushort> Blocks => _blocks;
    public ImmutableArray<ushort> BorderBlocks => _borderBlocks;
    public ImmutableArray<FieldWarp> FieldWarps => Events.GetProperty("warp_events").EnumerateArray().Select((e, index) =>
        new FieldWarp(index, e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(),
            _mapIdFromSymbol(e.GetProperty("dest_map").GetString()!),
            int.Parse(e.GetProperty("dest_warp_id").GetString()!, CultureInfo.InvariantCulture))).ToImmutableArray();

    private PretRuntimeMapData(PretRuntimeMapDocument data, ImmutableArray<ushort> blocks,
        ImmutableArray<ushort> borderBlocks, ImmutableDictionary<int, (uint Attributes, SourceRef Source)> metatiles,
        ImmutableDictionary<int, string> behaviors)
    {
        _data = data; _blocks = blocks; _borderBlocks = borderBlocks; _metatiles = metatiles; _behaviors = behaviors;
        Id = new(data.StableId);
        _mapIdFromSymbol = data.FullSourceHash switch
        {
            PretWorldMap.FullSourceHash => symbol => PretWorldCatalog.Specifications.SingleOrDefault(s => s.SourceMapSymbol == symbol) is { } spec
                ? new MapId(spec.StableId) : throw new InvalidDataException("Unknown base runtime map symbol: " + symbol),
            PretViridianRegion.FullSourceHash => PretViridianRegion.MapIdFromSourceSymbol,
            PretViridianCenter.FullSourceHash => PretViridianCenter.MapIdFromSourceSymbol,
            PretForestRegion.FullSourceHash => PretForestRegion.MapIdFromSourceSymbol,
            PretPewterRegion.FullSourceHash => PretPewterRegion.MapIdFromSourceSymbol,
            PretRoute22Region.FullSourceHash => PretRoute22Region.MapIdFromSourceSymbol,
            PretRoute3Region.FullSourceHash => PretRoute3Region.MapIdFromSourceSymbol,
            PretMtMoonRegion.FullSourceHash => PretMtMoonRegion.MapIdFromSourceSymbol,
            PretCeruleanRegion.FullSourceHash => PretCeruleanRegion.MapIdFromSourceSymbol,
            PretBillRegion.FullSourceHash => PretBillRegion.MapIdFromSourceSymbol,
            PretVermilionRegion.FullSourceHash => PretVermilionRegion.MapIdFromSourceSymbol,
            PretSsAnneRegion.FullSourceHash => PretSsAnneRegion.MapIdFromSourceSymbol,
            PretVermilionGymRegion.FullSourceHash => PretVermilionGymRegion.MapIdFromSourceSymbol,
            PretCityServices.FullSourceHash => PretCityServices.MapIdFromSourceSymbol,
            PretRoute11Region.FullSourceHash => PretRoute11Region.MapIdFromSourceSymbol,
            _ => throw new InvalidDataException("Unknown runtime map source profile.")
        };
    }

    public TileCell? GetTile(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return null;
        var block = PretWorldDecoder.DecodeBlock(_blocks[y * Width + x]);
        return new(block.Collision != 0, block.Elevation,
            PretWorldDecoder.Behavior((int)(_metatiles[block.MetatileId].Attributes & 511)));
    }

    public int? SourceBehaviorAt(int x, int y)
        => x < 0 || y < 0 || x >= Width || y >= Height ? null : (int)(_metatiles[_blocks[y * Width + x] & 1023].Attributes & 511);

    public PretWorldCell? GetSourceCell(int x, int y)
        => x < 0 || y < 0 || x >= Width || y >= Height ? null : Expand(_blocks[y * Width + x], x, y);

    public ImmutableArray<PretWorldCell> ExpandCells()
        => _blocks.Select((block, i) => Expand(block, i % Width, i / Width)).ToImmutableArray();

    public ImmutableArray<PretWorldCell> ExpandBorderCells()
        => _borderBlocks.Select((block, i) => Expand(block, i % BorderWidth, i / BorderWidth)).ToImmutableArray();

    private PretWorldCell Expand(ushort raw, int x, int y)
    {
        var block = PretWorldDecoder.DecodeBlock(raw);
        var metatile = _metatiles[block.MetatileId];
        var a = PretWorldDecoder.DecodeAttributes(metatile.Attributes);
        return new(x, y, raw, block.MetatileId, block.Collision, block.Elevation, metatile.Attributes,
            a.BehaviorId, _behaviors[a.BehaviorId], a.Terrain, a.Attribute2, a.Attribute3, a.EncounterType,
            a.Attribute5, a.LayerType, a.Attribute7, PretWorldDecoder.Movement(a.BehaviorId), metatile.Source);
    }

    // Called only by existing authenticated loaders. Keep their complete source inventory for
    // NPC identity checks/compiler references; never invent reduced Metadata.Files contents.
    internal PretWorldDocument ToWorldDocument(PretWorldMetadata authenticatedMetadata)
        => ToHeaderDocument(authenticatedMetadata) with { Cells = ExpandCells(), BorderCells = ExpandBorderCells() };

    // Compact-aware PretWorldMap adapters supply Cells/GetTile directly from this instance.
    // This header deliberately avoids allocating thousands of legacy provenance records.
    internal PretWorldDocument ToHeaderDocument(PretWorldMetadata authenticatedMetadata)
    {
        PretRuntimeMapValidation.Require(authenticatedMetadata.Repository == "pret/pokefirered" &&
            authenticatedMetadata.Commit == PretWorldMap.SourceCommit && authenticatedMetadata.FullSourceHash == FullSourceHash,
            "Runtime map metadata identity differs.");
        var embedded = Scripts.Select(s => new PretWorldEmbeddedSource(s.Path, s.Sha256,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(s.Text)))).ToImmutableArray();
        return new(1, Id.Value, SourceRef, authenticatedMetadata, Width, Height, TileSize, BorderWidth, BorderHeight,
            TexturePath, Artifacts, [], [], embedded, TechnicalSpawn, Events);
    }

    internal static PretRuntimeMapData Decode(byte[] bytes, PretRuntimeMapEntry expected)
    {
        try { return DecodeCore(bytes, expected); }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or ArgumentException or OverflowException)
        { throw new InvalidDataException("Invalid runtime map structure.", error); }
    }

    private static PretRuntimeMapData DecodeCore(byte[] bytes, PretRuntimeMapEntry expected)
    {
        var d = PretRuntimeMapValidation.Decode<PretRuntimeMapDocument>(bytes);
        void Require(bool condition, string message) => PretRuntimeMapValidation.Require(condition, message);
        Require(d.SchemaVersion == 1 && d.ConverterVersion == PretRuntimeMapCatalog.ConverterVersion &&
            d.SourceCommit == PretWorldMap.SourceCommit && d.StableId == expected.StableId &&
            d.SourceWorldSha256 == expected.SourceWorldSha256 && d.FullSourceHash == expected.FullSourceHash,
            "Runtime map version/source identity differs.");
        Require(d.Width is > 0 and <= 1024 && d.Height is > 0 and <= 1024 && (long)d.Width * d.Height <= 262144 &&
            d.TileSize == 16 && d.BorderWidth is > 0 and <= 64 && d.BorderHeight is > 0 and <= 64,
            "Runtime map dimensions out of bounds.");
        Require(d.SourceRef is not null && d.SourceRef.Kind == SourceKind.Pret && d.SourceRef.Repository == "pret/pokefirered" &&
            d.SourceRef.Commit == PretWorldMap.SourceCommit && PretRuntimeMapValidation.IsHash(d.SourceRef.Sha256) &&
            !string.IsNullOrWhiteSpace(d.SourceRef.Symbol) && d.SourceRef.Symbol.StartsWith("MAP_", StringComparison.Ordinal),
            "Runtime map source reference differs.");
        var source = d.SourceRef!;
        _ = PretRuntimeMapValidation.SafeFile(".", source.Path);
        Require(source.Path.StartsWith("data/maps/", StringComparison.Ordinal) && source.Path.EndsWith("/map.json", StringComparison.Ordinal),
            "Runtime map source path differs.");
        Require(!d.AttributeSources.IsDefaultOrEmpty && d.AttributeSources.Length <= 2 &&
            !d.Metatiles.IsDefaultOrEmpty && d.Metatiles.Length <= 1024 && !d.Behaviors.IsDefaultOrEmpty && d.Behaviors.Length <= 512,
            "Incomplete runtime map tables.");
        var sourcePaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in d.AttributeSources)
        {
            Require(s is not null && PretRuntimeMapValidation.IsHash(s.Sha256), "Invalid runtime attribute source.");
            _ = PretRuntimeMapValidation.SafeFile(".", s!.Path);
            Require(s.Path.StartsWith("data/tilesets/", StringComparison.Ordinal) && s.Path.EndsWith("/metatile_attributes.bin", StringComparison.Ordinal) &&
                sourcePaths.Add(s.Path), "Invalid/duplicate runtime attribute source.");
        }
        var behaviors = ImmutableDictionary.CreateBuilder<int, string>();
        foreach (var behavior in d.Behaviors)
            Require(behavior is not null && behavior.Id is >= 0 and <= 511 &&
                !string.IsNullOrWhiteSpace(behavior.Name) && behavior.Name.Length <= 100 &&
                behaviors.TryAdd(behavior.Id, behavior.Name), "Invalid/duplicate runtime behavior.");
        var metatiles = ImmutableDictionary.CreateBuilder<int, (uint Attributes, SourceRef Source)>();
        var usedSources = new HashSet<uint>();
        var usedBehaviors = new HashSet<int>();
        foreach (var entry in d.Metatiles)
        {
            Require(!entry.IsDefault && entry.Length == 3 && entry[0] <= 1023 && entry[2] < d.AttributeSources.Length,
                "Runtime metatile index out of bounds.");
            var id = (int)entry[0];
            var decoded = PretWorldDecoder.DecodeAttributes(entry[1]);
            Require(decoded.LayerType <= 2 && behaviors.ContainsKey(decoded.BehaviorId), "Runtime attribute layer/behavior is invalid.");
            var s = d.AttributeSources[(int)entry[2]];
            Require(s.Path.StartsWith(id < 640 ? "data/tilesets/primary/" : "data/tilesets/secondary/", StringComparison.Ordinal),
                "Runtime metatile tileset partition differs.");
            var localId = id < 640 ? id : id - 640;
            Require(metatiles.TryAdd(id, (entry[1], new(SourceKind.Pret, s.Path, $"metatile_{localId:x3}",
                source.Repository, source.Commit, s.Sha256))), "Duplicate runtime metatile ID.");
            usedSources.Add(entry[2]); usedBehaviors.Add(decoded.BehaviorId);
        }
        Require(usedSources.Count == d.AttributeSources.Length && usedBehaviors.SetEquals(behaviors.Keys), "Unused runtime source/behavior table entry.");
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
        ValidateEvents(d.Events, source.Symbol, d.Width, d.Height);
        if (d.TechnicalSpawn is { } spawn)
        {
            Require(spawn.X >= 0 && spawn.X < d.Width && spawn.Y >= 0 && spawn.Y < d.Height && spawn.Elevation is >= 0 and <= 15 &&
                !string.IsNullOrWhiteSpace(spawn.Reason), "Runtime spawn out of bounds.");
            var block = PretWorldDecoder.DecodeBlock(cells[spawn.Y * d.Width + spawn.X]);
            Require(block.Collision == 0 && block.Elevation == spawn.Elevation && (metatiles[block.MetatileId].Attributes & 511) == 0,
                "Runtime spawn is not an ordinary walkable source cell.");
        }
        Require(!d.Scripts.IsDefaultOrEmpty && d.Scripts.Length <= 2, "Missing runtime map scripts.");
        var scriptPaths = new HashSet<string>(StringComparer.Ordinal);
        var stem = source.Path[..^"map.json".Length];
        foreach (var script in d.Scripts)
            Require(script is not null && (script.Path == stem + "scripts.inc" || script.Path == stem + "text.inc") &&
                scriptPaths.Add(script.Path) && script.Text is not null && PretRuntimeMapValidation.IsHash(script.Sha256) &&
                PretRuntimeMapValidation.Hash(Encoding.UTF8.GetBytes(script.Text)) == script.Sha256, "Runtime script path/hash differs.");
        Require(scriptPaths.Contains(stem + "scripts.inc"), "Missing live runtime script dependency.");
        Require(d.TexturePath == "map.png" && !d.Artifacts.IsDefaultOrEmpty && d.Artifacts.Length <= 9, "Invalid runtime artifact inventory.");
        var artifacts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var artifact in d.Artifacts)
        {
            Require(artifact is not null && PretRuntimeMapValidation.IsHash(artifact.Sha256) && artifacts.Add(artifact.Path), "Invalid/duplicate runtime artifact.");
            var a = artifact!;
            Require(a.Path is "bottom.png" or "middle.png" or "top.png" or "map.png" or "player.png" or
                "border.png" or "border-bottom.png" or "border-middle.png" or "border-top.png", "Unknown runtime artifact path.");
            var size = a.Path == "player.png" ? (144, 32) : a.Path.StartsWith("border", StringComparison.Ordinal) ?
                (d.BorderWidth * 16, d.BorderHeight * 16) : (d.Width * 16, d.Height * 16);
            Require((a.Width, a.Height) == size, "Runtime artifact dimensions differ.");
        }
        Require(new[] { "bottom.png", "middle.png", "top.png", "map.png" }.All(artifacts.Contains), "Missing runtime visual layer.");
        return new(d, cells, border, metatiles.ToImmutable(), behaviors.ToImmutable());
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
                    Require(int.TryParse(e.GetProperty("dest_warp_id").GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) &&
                        id is >= 0 and <= 255, "Runtime warp index out of bounds.");
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
    internal const int MaximumMapBytes = 2 * 1024 * 1024;
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static bool IsHash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static string SafeFile(string root, string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\') &&
            !relative.Contains(':') && relative.Split('/').All(p => p is not ("" or "." or "..")), "Unsafe runtime map path.");
        return Path.Combine(Path.GetFullPath(root), relative);
    }
    internal static byte[] ReadBounded(string path, int maximum)
    {
        using var stream = File.OpenRead(path);
        Require(stream.Length is > 0 && stream.Length <= maximum, "Runtime map file size out of bounds.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        Require(stream.ReadByte() == -1, "Runtime map file changed while reading.");
        return bytes;
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
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
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
