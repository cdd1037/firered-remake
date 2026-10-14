using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Content;

/// <summary>Authenticated original Fly-map presentation. Travel permission stays in FieldFlyCatalog/Application.</summary>
public sealed class PretFlyMap
{
    public const string Profile = "firered-fly-map-v1";
    public const string SourceCommit = PretTownMap.SourceCommit;
    public const string SourceTree = PretTownMap.SourceTree;
    public const string LockSha256 = "596d183887dd9cbdc16de70091a82eb8b4739854c0a44137e713af3c486a55d4";
    public const string ManifestSha256 = "8f98f4499e9f71dc59fce594eb15f704183ea61b82968d0f4a08b9a07d5d48d4";
    public const string RawManifestSha256 = "22c363c66460cd530bd248f7221ad2ddd41be6c3cf6ca99f007732317a157326";
    public const string DocumentSha256 = "6a0be8f149dc14e0351d48b72632fa1f98189031b8947cba1333cd27305ecc55";
    public const string FullSourceHash = "79a9887ecaaf285fd0d224a659bbedcd941a8ca6f3a8f66e34e89360484fb45d";
    private static readonly string[] AssetIds = ["fly-surround", "fly-sevii45", "fly-sevii67", "fly-hide-navel", "fly-hide-birth", "fly-icon-0", "fly-icon-1"];
    private readonly ImmutableDictionary<string, byte[]> outputs;
    private readonly ImmutableDictionary<string, FlyMapSection> sections;
    public ImmutableArray<TownMapAsset> Assets { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<FlyMapRegion> Regions { get; }
    public ImmutableArray<FlyMapPositionOverride> PositionOverrides { get; }
    public ImmutableArray<int> FlyIconFrameTicks { get; }
    public ImmutableDictionary<string, string> UiTexts { get; }
    public bool SwitchAvailable => false;
    public ImmutableArray<string> AdditionalSourceHashes => ["fly-map-lock:" + LockSha256, "fly-map-manifest:" + ManifestSha256,
        .. SourceFiles.Select(f => "fly-map:" + f.Path + ":" + f.Sha256)];

    private PretFlyMap(JsonElement doc, ImmutableDictionary<string, byte[]> payloads, ImmutableArray<PretWorldSourceFile> source)
    {
        outputs = payloads; SourceFiles = source;
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true
        };
        T Parse<T>(string name) => JsonSerializer.Deserialize<T>(doc.GetProperty(name).GetRawText(), options)
            ?? throw new InvalidDataException("Null Fly map member: " + name);
        Need(doc.GetProperty("schemaVersion").GetInt32() == 1 && doc.GetProperty("profile").GetString() == Profile &&
            doc.GetProperty("sourceCommit").GetString() == SourceCommit && doc.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            !doc.GetProperty("switchAvailable").GetBoolean(), "Fly map document profile differs.");
        Assets = Parse<ImmutableArray<TownMapAsset>>("assets"); Regions = Parse<ImmutableArray<FlyMapRegion>>("regions");
        sections = Parse<ImmutableArray<FlyMapSection>>("sections").ToImmutableDictionary(s => s.SectionId, StringComparer.Ordinal);
        PositionOverrides = Parse<ImmutableArray<FlyMapPositionOverride>>("positionOverrides");
        FlyIconFrameTicks = Parse<ImmutableArray<int>>("flyIconFrameTicks");
        UiTexts = Parse<ImmutableDictionary<string, string>>("uiTexts");
        Need(Assets.Length == 7 && Assets.Select(a => a.Id).ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds) &&
            Regions.Length == 4 && sections.Count == 109 && PositionOverrides.Length == 27 && FlyIconFrameTicks.SequenceEqual([30, 60]),
            "Fly map inventory differs.");
        foreach (var a in Assets)
            Need(a.Width > 0 && a.Height > 0 && a.Width * a.Height <= 38400 && outputs[a.RgbaPath].Length == a.Width * a.Height * 4 &&
                Hash(outputs[a.RgbaPath]) == a.RgbaSha256 && Hash(outputs[a.TexturePath]) == a.Sha256 &&
                a.SourcePaths.Length > 0 && a.SourcePaths.All(p => source.Any(f => f.Path == p)), "Fly map asset metadata differs.");
        int[] dungeonCounts = [12, 2, 2, 4], destinationCounts = [13, 3, 2, 2];
        for (int region = 0; region < 4; region++)
        {
            var r = Regions[region];
            Need(r.Index == region && r.Cells.Length == 330 && r.DungeonIcons.Length == dungeonCounts[region] &&
                r.DestinationIcons.Length == destinationCounts[region], "Fly map region shape differs.");
            for (int i = 0; i < 330; i++) Need(r.Cells[i].X == i % 22 && r.Cells[i].Y == i / 22, "Fly map cell order differs.");
            foreach (var icon in r.DestinationIcons)
                Need(r.Cells[icon.Y * 22 + icon.X].SectionId == icon.SectionId && icon.CenterX == 36 + icon.X * 8 &&
                    icon.CenterY == 36 + icon.Y * 8 && icon.VisitedFlag.StartsWith("FLAG_WORLD_MAP_", StringComparison.Ordinal), "Fly destination icon differs.");
        }
        Need(Regions.SelectMany(r => r.DestinationIcons).Select(i => i.SectionId).Distinct(StringComparer.Ordinal).Count() == 20,
            "Fly map must have exactly twenty source destination sections.");
    }

    /// <summary>Authenticate every source Git blob and every derived pixel/data output before exposure.</summary>
    public static PretFlyMap Load(string projectRoot)
    {
        string project = Path.GetFullPath(projectRoot), raw = Path.Combine(project, "content/raw/fly-map"), generated = Path.Combine(project, "content/generated/fly-map");
        byte[] Pinned(string root, string relative, string expected, int? length = null, int maximum = 1048576)
        {
            var path = Safe(root, relative); var info = new FileInfo(path);
            Need(info.Exists && info.Length <= maximum && (length is null || info.Length == length), "Fly map file size differs: " + relative);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Need(stream.Length == info.Length, "Fly map file changed while opening: " + relative);
            byte[] bytes = new byte[checked((int)info.Length)]; stream.ReadExactly(bytes);
            Need(stream.ReadByte() == -1 && Hash(bytes) == expected, "Fly map file identity differs: " + relative);
            return bytes;
        }
        _ = Pinned(raw, "manifest.json", RawManifestSha256, maximum: 4096);
        using var sourceLock = JsonDocument.Parse(Pinned(project, "content/fly-map-source-lock.json", LockSha256, maximum: 65536));
        using var manifest = JsonDocument.Parse(Pinned(generated, "manifest.json", ManifestSha256, maximum: 32768));
        var l = sourceLock.RootElement; var m = manifest.RootElement;
        Need(l.GetProperty("schemaVersion").GetInt32() == 1 && l.GetProperty("repository").GetString() == "pret/pokefirered" &&
            l.GetProperty("commit").GetString() == SourceCommit && l.GetProperty("tree").GetString() == SourceTree &&
            l.GetProperty("profile").GetString() == Profile && l.GetProperty("gameEdition").GetString() == "FIRERED" &&
            l.GetProperty("romRevision").GetInt32() == 0, "Fly map source lock profile differs.");
        Need(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == SourceCommit && m.GetProperty("tree").GetString() == SourceTree &&
            m.GetProperty("lockSha256").GetString() == LockSha256 && m.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            m.GetProperty("inputCount").GetInt32() == 18 && m.GetProperty("assetCount").GetInt32() == 7 &&
            !m.GetProperty("upstreamScriptsExecuted").GetBoolean() && !m.GetProperty("romDownloaded").GetBoolean(), "Fly map manifest profile differs.");
        var sources = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var f in l.GetProperty("files").EnumerateArray())
        {
            string path = f.GetProperty("path").GetString()!, hash = f.GetProperty("sha256").GetString()!, blob = f.GetProperty("gitBlobSha1").GetString()!;
            int size = f.GetProperty("byteLength").GetInt32(); var bytes = Pinned(raw, path, hash, size);
            Need(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {size}\0"), .. bytes])) == blob, "Fly map source Git blob differs: " + path);
            sources.Add(new(path, blob, size, hash, $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{path}"));
        }
        Need(sources.Count == 18 && sources.Select(s => s.Path).Distinct(StringComparer.Ordinal).Count() == 18 &&
            Hash(Encoding.UTF8.GetBytes(string.Concat(sources.OrderBy(s => s.Path, StringComparer.Ordinal).Select(s => s.Path + "\0" + s.Sha256 + "\n")))) == FullSourceHash,
            "Fly map source inventory differs.");
        var payloads = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var o in m.GetProperty("outputs").EnumerateArray())
        {
            string path = o.GetProperty("path").GetString()!;
            Need(payloads.TryAdd(path, Pinned(generated, path, o.GetProperty("sha256").GetString()!, o.GetProperty("byteLength").GetInt32())), "Duplicate Fly map output.");
        }
        Need(payloads.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds.SelectMany(id => new[] { id + ".png", id + ".rgba" }).Append("fly-map.json")) &&
            Hash(payloads["fly-map.json"]) == DocumentSha256, "Fly map output inventory differs.");
        using var document = JsonDocument.Parse(payloads["fly-map.json"]);
        return new(document.RootElement, payloads.ToImmutable(), sources.ToImmutable());
    }

    public TownMapAsset Asset(string id) => Assets.Single(a => a.Id == id);
    public byte[] ReadAssetRgba(string id) => [.. outputs[Asset(id).RgbaPath]];
    public int? RegionForSection(string sectionId) => sections.TryGetValue(sectionId, out var section) ? section.Region : null;
    public FlyMapRegion Region(int region) => region is >= 0 and < 4 ? Regions[region] : throw new ArgumentOutOfRangeException(nameof(region));
    public TownMapCell CellAt(int region, int x, int y, IReadOnlySet<string> flags)
    {
        if (x is < 0 or >= 22 || y is < 0 or >= 15) throw new ArgumentOutOfRangeException(nameof(x));
        var cell = Region(region).Cells[y * 22 + x];
        // Preserve GetMapsecUnderCursor's original shared Navel flag check (including Birth).
        if (cell.SectionId is "MAPSEC_NAVEL_ROCK" or "MAPSEC_BIRTH_ISLAND" && !flags.Contains("FLAG_WORLD_MAP_NAVEL_ROCK_EXTERIOR"))
            cell = cell with { SectionId = "MAPSEC_NONE", Name = "" };
        if (cell.DungeonSectionId == "MAPSEC_CERULEAN_CAVE" && !flags.Contains("FLAG_SYS_CAN_LINK_WITH_RS"))
            cell = cell with { DungeonSectionId = "MAPSEC_NONE", DungeonName = "" };
        return cell;
    }
    public ImmutableArray<TownMapIcon> VisibleDungeonIcons(int region, IReadOnlySet<string> flags) =>
        [.. Region(region).DungeonIcons.Where(i => i.VisibleFlag is null || flags.Contains(i.VisibleFlag))];
    public ImmutableArray<TownMapIcon> VisibleDestinationIcons(int region, IReadOnlySet<string> flags) =>
        [.. Region(region).DestinationIcons.Where(i => flags.Contains(i.VisitedFlag))];
    public bool IsVisitedDestination(int region, string sectionId, IReadOnlySet<string> flags) =>
        Region(region).DestinationIcons.Any(i => i.SectionId == sectionId && flags.Contains(i.VisitedFlag));
    public string FlyIconAssetAt(long ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        return ticks % (FlyIconFrameTicks[0] + FlyIconFrameTicks[1]) < FlyIconFrameTicks[0] ? "fly-icon-0" : "fly-icon-1";
    }

    /// <summary>Reuse authenticated Kanto/123 pixels and one shared surround, with original event-island masks.</summary>
    public byte[] ReadBackgroundRgba(int region, IReadOnlySet<string> flags, PretTownMap townMap, PretFirstSeviiTownMap firstSevii)
    {
        _ = Region(region);
        byte[] frame = region switch
        {
            0 => townMap.ReadAssetRgba("kanto"), 1 => firstSevii.ReadAssetRgba("sevii123-locked"),
            2 => ReadAssetRgba("fly-sevii45"), _ => ReadAssetRgba("fly-sevii67")
        };
        void Overlay(string id, int left, int top)
        {
            var asset = Asset(id); var bytes = outputs[asset.RgbaPath];
            for (int y = 0; y < asset.Height; y++)
                for (int x = 0; x < asset.Width; x++)
                {
                    int source = (y * asset.Width + x) * 4, destination = ((y + top) * 240 + left + x) * 4;
                    if (bytes[source + 3] != 0) bytes.AsSpan(source, 4).CopyTo(frame.AsSpan(destination, 4));
                }
        }
        Overlay("fly-surround", 0, 0);
        if (region == 2 && !flags.Contains("FLAG_WORLD_MAP_NAVEL_ROCK_EXTERIOR")) Overlay("fly-hide-navel", 104, 88);
        if (region == 3 && !flags.Contains("FLAG_WORLD_MAP_BIRTH_ISLAND_EXTERIOR")) Overlay("fly-hide-birth", 168, 128);
        return frame;
    }

    /// <summary>Source outdoor projection using the already authenticated current map metadata. Never invent an indoor escape warp.</summary>
    public bool TryPositionForOutdoorMap(string sectionId, string sourceMap, string mapType, int width, int height, int x, int y, out TownMapPoint point)
    {
        point = new(21, 13);
        if (mapType is "MAP_TYPE_INDOOR" or "MAP_TYPE_UNDERGROUND" or "MAP_TYPE_UNKNOWN" or "MAP_TYPE_SECRET_BASE" ||
            !sections.TryGetValue(sectionId, out var section) || section.Width <= 0 || section.Height <= 0) return false;
        if (width <= 0 || height <= 0 || x < 0 || y < 0 || x >= width || y >= height) throw new ArgumentOutOfRangeException(nameof(x));
        var special = PositionOverrides.FirstOrDefault(p => p.SectionId == sectionId && (p.SourceMap is null || p.SourceMap == sourceMap));
        if (sectionId == "MAPSEC_ROUTE_21" && special is null) return false;
        point = special is not null ? new(special.X, special.Y) : new(
            section.X + Math.Min(x / Math.Max(1, width / section.Width), section.Width - 1),
            section.Y + Math.Min(y / Math.Max(1, height / section.Height), section.Height - 1));
        Need(point.X is >= 0 and < 22 && point.Y is >= 0 and < 15, "Fly map projection leaves source grid.");
        return true;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string Safe(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Fly map path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent) if (d.LinkTarget is not null) throw new InvalidDataException("Fly map root symlink.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null) throw new InvalidDataException("Fly map symlink.");
        }
        return result;
    }
}

public sealed record FlyMapRegion(int Index, ImmutableArray<TownMapCell> Cells, ImmutableArray<TownMapIcon> DungeonIcons, ImmutableArray<TownMapIcon> DestinationIcons);
public sealed record FlyMapSection(string SectionId, int Region, int X, int Y, int Width, int Height);
public sealed record FlyMapPositionOverride(string SectionId, string? SourceMap, int X, int Y);
