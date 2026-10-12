using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>Authenticated two-page first-Sevii atlas extension. It grants no travel or unlock flags.</summary>
public sealed class PretFirstSeviiTownMap
{
    public const string Profile = "firered-first-sevii-town-map-v1";
    public const string SourceCommit = PretTownMap.SourceCommit;
    public const string SourceTree = PretTownMap.SourceTree;
    public const string LockSha256 = "6340ef420abcd5f601c26098368e7360b894ed598afb7fd3b3f5cbfcf51e0bb3";
    public const string ManifestSha256 = "ceb256f9e8966259c52bc9c973d6fea10410f356fb61575c1846f77abd8b75ef";
    public const string RawManifestSha256 = "5111a410ccecd25fffe8b9568368560b85224e733e4f945dc1435d581d32b48c";
    public const string TownMapSha256 = "453cb8f024981757ff8d1eac8ed018930a58c4ad0615228cd0724f383a2af389";
    public const string FullSourceHash = "004c73ec53e1c402f982d2b44fef6c7d54c66570161f714c70b4b8e39b7bba07";
    public const string UnlockFlag = "FLAG_SYS_SEVII_MAP_123";
    private static readonly string[] AssetIds = ["kanto-switch", "sevii123", "sevii123-locked", "switch-menu", "switch-left-0", "switch-left-1", "switch-right-0", "switch-right-1", "keypad-updown"];
    private readonly ImmutableDictionary<string, byte[]> outputs;
    private readonly ImmutableDictionary<string, FirstSeviiTownMapPosition> positionsByMap;
    public ImmutableArray<TownMapAsset> Assets { get; }
    public ImmutableArray<TownMapCell> Cells { get; }
    public ImmutableArray<TownMapIcon> Icons { get; }
    public ImmutableArray<FirstSeviiTownMapPosition> Positions { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    public FirstSeviiTownMapSwitchMenu SwitchMenu { get; }

    private PretFirstSeviiTownMap(JsonElement d, ImmutableDictionary<string, byte[]> payloads, ImmutableArray<PretWorldSourceFile> files)
    {
        T Parse<T>(string property) => ContentJson.ParseStrict<T>(d.GetProperty(property).GetRawText(), ContentJson.CreateOptions());
        outputs = payloads; SourceFiles = files;
        Assets = Parse<ImmutableArray<TownMapAsset>>("assets"); Cells = Parse<ImmutableArray<TownMapCell>>("cells");
        Icons = Parse<ImmutableArray<TownMapIcon>>("icons"); Positions = Parse<ImmutableArray<FirstSeviiTownMapPosition>>("positions");
        SwitchMenu = Parse<FirstSeviiTownMapSwitchMenu>("switchMenu");
        positionsByMap = Positions.ToImmutableDictionary(p => p.StableMapId, StringComparer.Ordinal);
        AdditionalSourceHashes = ["first-sevii-town-map-lock:" + LockSha256, "first-sevii-town-map-manifest:" + ManifestSha256,
            .. files.Select(f => "first-sevii-town-map:" + f.Path + ":" + f.Sha256)];
        Need(d.GetProperty("profile").GetString() == Profile && d.GetProperty("sourceCommit").GetString() == SourceCommit &&
            d.GetProperty("fullSourceHash").GetString() == FullSourceHash && d.GetProperty("unlockFlag").GetString() == UnlockFlag,
            "First-Sevii Town Map document profile differs.");
        Need(Assets.Length == 9 && Assets.Select(a => a.Id).ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds), "First-Sevii atlas inventory differs.");
        foreach (var a in Assets)
            Need(a.Width > 0 && a.Height > 0 && a.Width * a.Height <= 38400 && outputs[a.RgbaPath].Length == a.Width * a.Height * 4 &&
                Hash(outputs[a.RgbaPath]) == a.RgbaSha256 && Hash(outputs[a.TexturePath]) == a.Sha256 &&
                a.SourcePaths.Length > 0 && a.SourcePaths.All(p => files.Any(f => f.Path == p)), "First-Sevii atlas asset differs.");
        Need(Cells.Length == 330 && Icons.Length == 2 && Positions.Length == 16 &&
            SwitchMenu == new FirstSeviiTownMapSwitchMenu(1, 6, 72, 168, 32, 88, 152, 32, 20, true), "First-Sevii atlas geometry differs.");
        for (int i = 0; i < Cells.Length; i++) Need(Cells[i].X == i % 22 && Cells[i].Y == i / 22, "First-Sevii atlas cell order differs.");
        foreach (var p in Positions)
            Need(p.Width > 0 && p.Height > 0 && p.SectionWidth > 0 && p.SectionHeight > 0 && p.Region is 0 or 1 &&
                p.SourcePaths.All(path => files.Any(f => f.Path == path)), "First-Sevii atlas position differs.");
        Need(Positions.Count(p => p.RequiresEscapeWarp) == 1 && positionsByMap["map.three-island-harbor"].RequiresEscapeWarp,
            "First-Sevii indoor escape-warp boundary differs.");
    }

    public static PretFirstSeviiTownMap Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot); var raw = Path.Combine(root, "content/raw/first-sevii-town-map");
        var generated = Path.Combine(root, "content/generated/first-sevii-town-map");
        byte[] Pinned(string directory, string relative, string hash, int? length = null, int maximum = 1048576)
        {
            var path = Safe(directory, relative); var info = new FileInfo(path);
            Need(info.Exists && info.Length <= maximum && (length is null || info.Length == length), "First-Sevii atlas file size differs: " + relative);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Need(stream.Length == info.Length, "First-Sevii atlas file changed.");
            var bytes = new byte[checked((int)info.Length)]; stream.ReadExactly(bytes);
            Need(stream.ReadByte() == -1 && Hash(bytes) == hash, "First-Sevii atlas file identity differs: " + relative); return bytes;
        }
        _ = Pinned(raw, "manifest.json", RawManifestSha256, maximum: 4096);
        using var sourceLock = JsonDocument.Parse(Pinned(root, "content/first-sevii-town-map-source-lock.json", LockSha256, maximum: 65536));
        using var manifest = JsonDocument.Parse(Pinned(generated, "manifest.json", ManifestSha256, maximum: 32768));
        var l = sourceLock.RootElement; var m = manifest.RootElement;
        Need(l.GetProperty("schemaVersion").GetInt32() == 1 && l.GetProperty("repository").GetString() == "pret/pokefirered" &&
            l.GetProperty("commit").GetString() == SourceCommit && l.GetProperty("tree").GetString() == SourceTree &&
            l.GetProperty("profile").GetString() == Profile && l.GetProperty("gameEdition").GetString() == "FIRERED" &&
            l.GetProperty("romRevision").GetInt32() == 0, "First-Sevii atlas source profile differs.");
        Need(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == SourceCommit && m.GetProperty("tree").GetString() == SourceTree &&
            m.GetProperty("lockSha256").GetString() == LockSha256 && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("inputCount").GetInt32() == 38 && m.GetProperty("assetCount").GetInt32() == 9 &&
            !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean(), "First-Sevii atlas manifest differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var f in l.GetProperty("files").EnumerateArray())
        {
            var path = f.GetProperty("path").GetString()!; var sha = f.GetProperty("sha256").GetString()!;
            var blob = f.GetProperty("gitBlobSha1").GetString()!; var size = f.GetProperty("byteLength").GetInt32();
            var bytes = Pinned(raw, path, sha, size);
            Need(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {size}\0"), .. bytes])) == blob, "First-Sevii atlas Git blob differs.");
            files.Add(new(path, blob, size, sha, $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        Need(files.Count == 38 && files.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() == 38 &&
            Hash(Encoding.UTF8.GetBytes(string.Concat(files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + f.Sha256 + "\n")))) == FullSourceHash,
            "First-Sevii atlas source inventory differs.");
        var payloads = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var o in m.GetProperty("outputs").EnumerateArray())
        {
            var p = o.GetProperty("path").GetString()!;
            Need(payloads.TryAdd(p, Pinned(generated, p, o.GetProperty("sha256").GetString()!, o.GetProperty("byteLength").GetInt32())), "Duplicate atlas output.");
        }
        Need(payloads.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds.SelectMany(id => new[] { id + ".png", id + ".rgba" }).Append("town-map.json")) &&
            Hash(payloads["town-map.json"]) == TownMapSha256, "First-Sevii atlas output inventory differs.");
        using var document = JsonDocument.Parse(payloads["town-map.json"]);
        return new(document.RootElement, payloads.ToImmutable(), files.ToImmutable());
    }

    public TownMapAsset Asset(string id) => Assets.Single(a => a.Id == id);
    public byte[] ReadAssetRgba(string id) => [.. outputs[Asset(id).RgbaPath]];
    public TownMapCell CellAt(int x, int y)
    {
        if (x is < 0 or >= 22 || y is < 0 or >= 15) throw new ArgumentOutOfRangeException(nameof(x));
        return Cells[y * 22 + x];
    }
    public int? RegionForMap(string mapId) => positionsByMap.TryGetValue(mapId, out var p) ? p.Region : null;
    public bool TryPositionForMap(string mapId, int x, int y, out TownMapPoint point)
    {
        point = new(21, 11);
        if (!positionsByMap.TryGetValue(mapId, out var p)) return false;
        if (x < 0 || y < 0 || x >= p.Width || y >= p.Height) throw new ArgumentOutOfRangeException(nameof(x));
        // The campaign's bounded Rope destination is not the original general indoor escapeWarp.
        if (p.RequiresEscapeWarp) return false;
        point = p.Override ?? new(Math.Min(x / Math.Max(1, p.Width / p.SectionWidth), p.SectionWidth - 1) + p.SectionX,
            Math.Min(y / Math.Max(1, p.Height / p.SectionHeight), p.SectionHeight - 1) + p.SectionY);
        return true;
    }
    private static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
    private static void Need(bool value, string message) { if (!value) throw new InvalidDataException(message); }
    private static string Safe(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Unsafe first-Sevii atlas path.");
        for (var dir = new DirectoryInfo(root); dir is not null; dir = dir.Parent)
            if (dir.LinkTarget is not null) throw new InvalidDataException("First-Sevii atlas root symlink.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("First-Sevii atlas symlink.");
        }
        return result;
    }
}

public sealed record FirstSeviiTownMapPosition(string StableMapId, string SourceName, string SourceMap, string SectionId,
    int Width, int Height, int SectionX, int SectionY, int SectionWidth, int SectionHeight, string MapType,
    TownMapPoint? Override, int Region, bool RequiresEscapeWarp, ImmutableArray<string> SourcePaths);
public sealed record FirstSeviiTownMapSwitchMenu(int MaxSelection, int YOffset, int HighlightLeft, int HighlightRight,
    int HighlightHeight, int LeftArrowX, int RightArrowX, int ArrowSize, int ArrowFrameTicks, bool CancelRestoresPlayerRegion);
