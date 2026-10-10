using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed record PretWorldSourceFile(string Path, string GitBlobSha1, int ByteLength, string Sha256, string SourceUrl);
public sealed record PretWorldMetadata(string Repository, string Commit, string FullSourceHash, string Rights,
    string Scope, string RenderProfile, ImmutableArray<PretWorldSourceFile> Files);
public sealed record PretWorldArtifact(string Path, string Sha256, int Width, int Height);
public sealed record PretWorldEmbeddedSource(string Path, string Sha256, string Base64);
public sealed record PretWorldSpawn(int X, int Y, int Elevation, string Reason);
public sealed record PretWorldAttributes(int BehaviorId, int Terrain, int Attribute2, int Attribute3,
    int EncounterType, int Attribute5, int LayerType, int Attribute7);
public sealed record PretWorldCell(int X, int Y, ushort RawBlock, int MetatileId, int Collision, int Elevation,
    uint RawAttributes, int BehaviorId, string BehaviorName, int Terrain, int Attribute2, int Attribute3,
    int EncounterType, int Attribute5, int LayerType, int Attribute7, string Movement, SourceRef SourceRef);
internal sealed record PretWorldDocument(int SchemaVersion, string StableId, SourceRef SourceRef,
    PretWorldMetadata Metadata, int Width, int Height, int TileSize, int BorderWidth, int BorderHeight,
    string TexturePath, ImmutableArray<PretWorldArtifact> Artifacts, ImmutableArray<PretWorldCell> Cells,
    ImmutableArray<PretWorldCell> BorderCells, ImmutableArray<PretWorldEmbeddedSource> EmbeddedSource,
    PretWorldSpawn? TechnicalSpawn, JsonElement Events);

/// <summary>Pure little-endian FireRed field-map bit decoding; unknown behaviors remain unsupported.</summary>
public static class PretWorldDecoder
{
    public static (int MetatileId, int Collision, int Elevation) DecodeBlock(ushort raw)
        => (raw & 0x03ff, (raw >> 10) & 3, raw >> 12);
    public static PretWorldAttributes DecodeAttributes(uint raw)
        => new((int)(raw & 0x1ff), (int)((raw >> 9) & 31), (int)((raw >> 14) & 15),
            (int)((raw >> 18) & 63), (int)((raw >> 24) & 7), (int)((raw >> 27) & 3),
            (int)((raw >> 29) & 3), (int)(raw >> 31));
    public static string Movement(int behavior) => behavior switch
    {
        0x00 => "normal", 0x02 => "grassUnsupported", >= 0x38 and <= 0x3b => "ledgeUnsupported",
        0x69 => "doorUnsupported", >= 0x62 and <= 0x65 => "arrowWarpUnsupported",
        >= 0x6c and <= 0x6f => "stairWarpUnsupported",
        0x60 or 0x61 or 0x66 or 0x67 or 0x68 or 0x71 => "warpUnsupported", 0x10 or 0x11 or 0x12 or 0x13 or 0x15 => "waterUnsupported",
        0x84 => "signBlocked", _ => "unsupported"
    };
    public static TileBehavior Behavior(int behavior) => behavior switch
    {
        0x00 => TileBehavior.Floor, 0x02 => TileBehavior.Grass,
        >= 0x38 and <= 0x3b => TileBehavior.Ledge, >= 0x62 and <= 0x65 => TileBehavior.ArrowWarp, >= 0x6c and <= 0x6f => TileBehavior.StairWarp, >= 0x60 and <= 0x69 or 0x71 => TileBehavior.Warp, 0x84 => TileBehavior.Sign,
        _ => TileBehavior.Unsupported
    };
}

/// <summary>
/// Public loading retains the pinned five-map Pallet Town adapter. An internal authenticated region factory is separate.
/// Door/water/sign fields stay distinct; source events are inventory, never executable scripts.
/// This adapter does not execute original story progression, NPCs, warps, connections or animation.
/// </summary>
public sealed class PretWorldMap : IFieldMap
{
    public const string FullSourceHash = "8795cbf31a2b799b33428fa6f88207a6f33a46574e5a4195bb9f79018ef088d4";
    public const string SourceInventoryHash = "2157e54e07dd6ed7bc977d6ddd7acaf843c93427cbbbf40f20fb837b58ee7d26";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    private readonly PretWorldDocument _data;
    private readonly PretRuntimeMapData? _compact;
    public bool UsesCompactData => _compact is not null;
    public MapId Id { get; }
    public int? SourceEncounterTypeAt(int x,int y) => GetSourceCell(x,y)?.EncounterType;
    public int? SourceBehaviorAt(int x,int y) => _compact is null ? GetSourceCell(x,y)?.BehaviorId : _compact.SourceBehaviorAt(x,y);
    public ImmutableArray<FieldWarp> FieldWarps => Warps.Select(w=>new FieldWarp(w.Index,w.X,w.Y,w.Elevation,w.DestinationMapId,w.DestinationWarpId)).ToImmutableArray();
    public int Width => _data.Width;
    public int Height => _data.Height;
    public int TileSize => _data.TileSize;
    public int BorderWidth => _data.BorderWidth;
    public int BorderHeight => _data.BorderHeight;
    public SourceRef SourceRef => _data.SourceRef;
    public PretWorldMetadata Metadata => _data.Metadata;
    public PretWorldSpawn TechnicalSpawn => _data.TechnicalSpawn ?? throw new InvalidDataException("No technical spawn is declared for this authenticated region map.");
    public ImmutableArray<PretWorldCell> Cells => _compact?.ExpandCells() ?? _data.Cells;
    public ImmutableArray<PretWorldCell> BorderCells => _compact?.ExpandBorderCells() ?? _data.BorderCells;
    public ImmutableArray<PretWorldArtifact> Artifacts => _data.Artifacts;
    public JsonElement Events => _data.Events;
    public string SourceMapSymbol => SourceRef.Symbol;
    public string ScriptSource => EmbeddedText(SourceRef.Path.Replace("map.json", "scripts.inc", StringComparison.Ordinal));
    public string TextSource => EmbeddedText(SourceRef.Path.Replace("map.json", "text.inc", StringComparison.Ordinal));
    private string EmbeddedText(string path) => Encoding.UTF8.GetString(Convert.FromBase64String(_data.EmbeddedSource.Single(e => e.Path == path).Base64));
    public ImmutableArray<PretWorldWarp> Warps => Events.GetProperty("warp_events").EnumerateArray().Select((e, index) =>
        new PretWorldWarp(index, e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(),
            _mapIdFromSymbol(e.GetProperty("dest_map").GetString()!),
            PretRocketHideoutRegion.ParseWarpId(Metadata.FullSourceHash, SourceMapSymbol, e))).ToImmutableArray();
    public static ImmutableDictionary<MapId, PretWorldMap> LoadCatalog(string collectionRoot) => PretWorldCatalog.Load(collectionRoot);
    public string DirectoryPath { get; }
    public string TexturePath => Path.Combine(DirectoryPath, _data.TexturePath);
    public string BottomTexturePath => Path.Combine(DirectoryPath, "bottom.png");
    public string MiddleTexturePath => Path.Combine(DirectoryPath, "middle.png");
    public string TopTexturePath => Path.Combine(DirectoryPath, "top.png");
    public string PlayerTexturePath => _playerTexturePath ?? Path.Combine(DirectoryPath, "player.png");
    private readonly string? _playerTexturePath;
    private readonly Func<string, MapId> _mapIdFromSymbol;
    private PretWorldMap(PretWorldDocument data, string directory, Func<string, MapId>? mapIdFromSymbol = null, string? playerTexturePath = null, PretRuntimeMapData? compact = null)
    {
        _compact = compact; _data = data; DirectoryPath = directory; Id = new(data.StableId); _playerTexturePath = playerTexturePath;
        _mapIdFromSymbol = mapIdFromSymbol ?? (symbol => PretWorldCatalog.Specifications.FirstOrDefault(s => s.SourceMapSymbol == symbol) is { } spec
            ? new MapId(spec.StableId) : throw new InvalidDataException("Warp source map is outside the authenticated collection: " + symbol));
    }
    // Only the separate region loader calls this after authenticating its fixed manifest, every output,
    // and the complete fixed source inventory. This never routes region data through the five-map lock.
    internal static PretWorldMap FromAuthenticatedRegion(PretWorldDocument data, string directory, string playerTexturePath)
        => new(data, directory, PretViridianRegion.MapIdFromSourceSymbol, playerTexturePath);
    // Authenticated extensions supply their own closed source-symbol resolver and verify
    // their separate manifest before constructing a map. Existing profiles stay unchanged.
    internal static PretWorldMap FromAuthenticatedExtension(PretWorldDocument data, string directory, string playerTexturePath, Func<string, MapId> mapIdFromSourceSymbol)
        => new(data, directory, mapIdFromSourceSymbol, playerTexturePath);
    internal static PretWorldMap FromRuntime(PretRuntimeMapData compact, PretWorldMetadata metadata, string directory, string? playerTexturePath = null, Func<string, MapId>? resolver = null)
        => new(compact.ToHeaderDocument(metadata), directory, resolver, playerTexturePath, compact);
    public PretWorldCell? GetSourceCell(int x, int y)
        => _compact is not null ? _compact.GetSourceCell(x,y) : x < 0 || y < 0 || x >= Width || y >= Height ? null : _data.Cells[y * Width + x];
    public TileCell? GetTile(int x, int y)
    {
        if (_compact is not null) return _compact.GetTile(x,y);
        var c = GetSourceCell(x, y);
        return c is null ? null : new(c.Collision != 0, c.Elevation, PretWorldDecoder.Behavior(c.BehaviorId));
    }
    public static PretWorldMap Load(string generatedDirectory)
    {
        var dir = Path.GetFullPath(generatedDirectory);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        var d = JsonSerializer.Deserialize<PretWorldDocument>(File.ReadAllBytes(Path.Combine(dir, "world.json")), options)
            ?? throw new InvalidDataException("Pret world JSON is null.");
        Validate(d, dir);
        return new(d, dir);
    }
    private static void Validate(PretWorldDocument d, string directory)
    {
        static void Require(bool test, string message) { if (!test) throw new InvalidDataException(message); }
        static string Hash(ReadOnlySpan<byte> b) => Convert.ToHexStringLower(SHA256.HashData(b));
        static bool SafePath(string p) => !string.IsNullOrWhiteSpace(p) && !Path.IsPathRooted(p)
            && !p.Contains('\\') && p.Split('/').All(s => s is not ("" or "." or ".."));
        var spec = PretWorldCatalog.Specifications.SingleOrDefault(s => s.StableId == d.StableId);
        Require(d.SchemaVersion == 1 && spec is not null, "Unsupported map/schema.");
        var expected = spec!;
        var sourceMapPath = $"data/maps/{expected.SourceName}/map.json";
        Require(d.Metadata is not null && d.SourceRef is not null, "Missing source metadata.");
        var metadata = d.Metadata!;
        Require(metadata.Repository == "pret/pokefirered" && metadata.Commit == SourceCommit, "Unexpected source identity.");
        Require(d.Width > 0 && d.Height > 0 && d.TileSize == 16 && d.BorderWidth > 0 && d.BorderHeight > 0, "Invalid map dimensions.");
        Require(!d.Cells.IsDefault && d.Cells.Length == (long)d.Width * d.Height && !d.BorderCells.IsDefault && d.BorderCells.Length == (long)d.BorderWidth * d.BorderHeight, "Incomplete map grid.");
        Require(!metadata.Files.IsDefaultOrEmpty && metadata.Files.All(f => f is not null && SafePath(f.Path)), "Missing/unsafe evidence files.");
        var files = metadata.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        Require(files.Count == metadata.Files.Length, "Duplicate evidence path.");
        var material = string.Concat(files.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => f.Key + "\0" + f.Value.Sha256 + "\n"));
        Require(metadata.FullSourceHash == FullSourceHash && Hash(Encoding.UTF8.GetBytes(material)) == metadata.FullSourceHash, "Source set hash mismatch.");
        var inventory = string.Concat(files.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f =>
            f.Key + "\0" + f.Value.GitBlobSha1 + "\0" + f.Value.ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\0" + f.Value.Sha256 + "\0" + f.Value.SourceUrl + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(inventory)) == SourceInventoryHash, "Pinned source metadata inventory differs.");
        Require(d.SourceRef!.Kind == SourceKind.Pret && d.SourceRef.Commit == SourceCommit && d.SourceRef.Repository == metadata.Repository
            && d.SourceRef.Path == sourceMapPath && d.SourceRef.Symbol == expected.SourceMapSymbol
            && d.SourceRef.Sha256 == files[d.SourceRef.Path].Sha256, "Map SourceRef mismatch.");
        Require(!d.EmbeddedSource.IsDefaultOrEmpty, "Missing original binary evidence.");
        var embedded = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var e in d.EmbeddedSource)
        {
            Require(e is not null && files.ContainsKey(e.Path), "Unknown binary evidence.");
            var b = Convert.FromBase64String(e!.Base64); var f = files[e.Path];
            var header = Encoding.ASCII.GetBytes($"blob {b.Length}\0");
            Require(b.Length == f.ByteLength && Hash(b) == f.Sha256 && e.Sha256 == f.Sha256 &&
                Convert.ToHexStringLower(SHA1.HashData([.. header, .. b])) == f.GitBlobSha1, "Original binary evidence hash mismatch.");
            Require(embedded.TryAdd(e.Path, b), "Duplicate binary evidence.");
        }
        using var sourceEvents = JsonDocument.Parse(embedded[sourceMapPath]);
        Require(JsonElement.DeepEquals(d.Events, sourceEvents.RootElement), "Source event inventory differs from original bytes.");
        using var sourceLayouts = JsonDocument.Parse(embedded["data/layouts/layouts.json"]);
        string layoutId = sourceEvents.RootElement.GetProperty("layout").GetString()!;
        var layout = sourceLayouts.RootElement.GetProperty("layouts").EnumerateArray().Single(l => l.TryGetProperty("id", out var id) && id.GetString() == layoutId);
        Require(layoutId == expected.SourceMapSymbol.Replace("MAP_", "LAYOUT_", StringComparison.Ordinal), "Source layout identity differs.");
        Require(d.Width == layout.GetProperty("width").GetInt32() && d.Height == layout.GetProperty("height").GetInt32()
            && d.BorderWidth == layout.GetProperty("border_width").GetInt32() && d.BorderHeight == layout.GetProperty("border_height").GetInt32(), "Pinned source layout dimensions differ.");
        string mapPath = layout.GetProperty("blockdata_filepath").GetString()!, borderPath = layout.GetProperty("border_filepath").GetString()!;
        string primaryBase = PretWorldCatalog.TilesetBase(layout.GetProperty("primary_tileset").GetString()!);
        string secondaryBase = PretWorldCatalog.TilesetBase(layout.GetProperty("secondary_tileset").GetString()!);
        var requiredEvidence = new HashSet<string>([sourceMapPath, $"data/maps/{expected.SourceName}/scripts.inc", $"data/maps/{expected.SourceName}/text.inc",
            "data/layouts/layouts.json", "include/constants/metatile_behaviors.h", mapPath, borderPath,
            primaryBase + "/metatile_attributes.bin", secondaryBase + "/metatile_attributes.bin"], StringComparer.Ordinal);
        Require(requiredEvidence.SetEquals(embedded.Keys), "Original evidence inventory differs.");
        var behaviorNames = Regex.Matches(Encoding.UTF8.GetString(embedded["include/constants/metatile_behaviors.h"]), @"^#define\s+(MB_\w+)\s+(0x[0-9A-Fa-f]+)", RegexOptions.Multiline)
            .ToDictionary(m => Convert.ToInt32(m.Groups[2].Value, 16), m => m.Groups[1].Value);
        void ValidateCells(ImmutableArray<PretWorldCell> cells, byte[] bytes, int width)
        {
            Require(bytes.Length == cells.Length * 2, "Block byte count differs.");
            for (var i = 0; i < cells.Length; i++)
            {
                var c = cells[i]; Require(c is not null && c.X == i % width && c.Y == i / width, "Cell coordinate mismatch.");
                var raw = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2, 2));
                var block = PretWorldDecoder.DecodeBlock(raw);
                Require(c!.RawBlock == raw && (c.MetatileId, c.Collision, c.Elevation) == block, "Block decoder mismatch.");
                bool primary = block.MetatileId < 640; int local = primary ? block.MetatileId : block.MetatileId - 640;
                var path = (primary ? primaryBase : secondaryBase) + "/metatile_attributes.bin";
                var attrBytes = embedded[path]; Require(local * 4 + 4 <= attrBytes.Length, "Unknown metatile.");
                uint a = BinaryPrimitives.ReadUInt32LittleEndian(attrBytes.AsSpan(local * 4, 4));
                var decoded = PretWorldDecoder.DecodeAttributes(a);
                Require(c.RawAttributes == a && decoded == new PretWorldAttributes(c.BehaviorId, c.Terrain, c.Attribute2,
                    c.Attribute3, c.EncounterType, c.Attribute5, c.LayerType, c.Attribute7), "Metatile attributes decoder mismatch.");
                Require(c.BehaviorName == behaviorNames.GetValueOrDefault(c.BehaviorId, $"UNKNOWN_0x{c.BehaviorId:X3}"), "Behavior symbol differs from source constants.");
                Require(c.LayerType is >= 0 and <= 2 && c.Movement == PretWorldDecoder.Movement(c.BehaviorId), "Unsupported layer/classification mismatch.");
                Require(c.SourceRef is not null && c.SourceRef.Kind == SourceKind.Pret && c.SourceRef.Commit == SourceCommit
                    && c.SourceRef.Repository == metadata.Repository && c.SourceRef.Path == path && c.SourceRef.Symbol == $"metatile_{local:x3}"
                    && c.SourceRef.Sha256 == files[path].Sha256, "Metatile SourceRef mismatch.");
            }
        }
        ValidateCells(d.Cells, embedded[mapPath], d.Width); ValidateCells(d.BorderCells, embedded[borderPath], d.BorderWidth);
        Require(!d.Artifacts.IsDefaultOrEmpty && d.TexturePath == "map.png", "Missing visual artifact metadata.");
        var artifactMaterial = string.Concat(d.Artifacts.OrderBy(a => a.Path, StringComparer.Ordinal).Select(a =>
            a.Path + "\0" + a.Sha256 + "\0" + a.Width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\0" + a.Height.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n"));
        Require(Hash(Encoding.UTF8.GetBytes(artifactMaterial)) == PretWorldCatalog.ArtifactSetHash(d.StableId), "Pinned render artifact set differs.");
        var expectedNames = new HashSet<string>(["bottom.png", "middle.png", "top.png", "map.png", "player.png"], StringComparer.Ordinal);
        foreach (var a in d.Artifacts)
        {
            Require(a is not null && expectedNames.Remove(a.Path), "Unknown/duplicate artifact.");
            var bytes = File.ReadAllBytes(Path.Combine(directory, a!.Path));
            Require(Hash(bytes) == a.Sha256 && bytes.Length > 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "Visual hash/PNG mismatch.");
            Require(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16, 4)) == a.Width && BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20, 4)) == a.Height,
                "PNG dimensions mismatch.");
            Require(a.Path == "player.png" ? a.Width == 144 && a.Height == 32 : a.Width == d.Width * 16 && a.Height == d.Height * 16, "Unexpected visual dimensions.");
        }
        Require(expectedNames.Count == 0, "Incomplete artifacts.");
        Require(d.Events.ValueKind == JsonValueKind.Object && d.Events.GetProperty("id").GetString() == expected.SourceMapSymbol, "Source event inventory identity differs.");
        var entrance = d.Events.GetProperty("warp_events")[0];
        (int Dx, int Dy)[] offsets = d.StableId == "map.pallet-town" ? [(0, 1)] : [(0, -1), (-1, 0), (0, 1), (1, 0)];
        var anchor = offsets.Select(o => (X: entrance.GetProperty("x").GetInt32() + o.Dx, Y: entrance.GetProperty("y").GetInt32() + o.Dy))
            .First(p => p.X >= 0 && p.Y >= 0 && p.X < d.Width && p.Y < d.Height && d.Cells[p.Y * d.Width + p.X] is { Collision: 0, Elevation: 3, BehaviorId: 0 });
        Require(d.TechnicalSpawn is { Elevation: 3 } && d.TechnicalSpawn.X == anchor.X && d.TechnicalSpawn.Y == anchor.Y, "Unsafe or changed technical spawn.");
        foreach (var warp in d.Events.GetProperty("warp_events").EnumerateArray())
        {
            int x = warp.GetProperty("x").GetInt32(), y = warp.GetProperty("y").GetInt32();
            Require(x >= 0 && x < d.Width && y >= 0 && y < d.Height, "Warp outside source layout.");
            Require(PretWorldCatalog.Specifications.Any(s => s.SourceMapSymbol == warp.GetProperty("dest_map").GetString()), "Warp leaves bounded map collection.");
            Require(int.TryParse(warp.GetProperty("dest_warp_id").GetString(), out int id) && id >= 0, "Non-static warp unsupported in bounded collection.");
        }
    }
}
