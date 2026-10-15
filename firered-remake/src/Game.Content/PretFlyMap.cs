using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Content;

/// <summary>Generated Fly-map presentation. Travel permission stays in FieldFlyCatalog/Application.</summary>
public sealed class PretFlyMap
{
    public const string Profile = "firered-fly-map-v1";
    public const string SourceCommit = PretTownMap.SourceCommit;
    private static readonly string[] AssetIds = ["fly-icon-0", "fly-icon-1"];
    private readonly ImmutableDictionary<string, FlyMapSection> sections;
    public ImmutableArray<TownMapAsset> Assets { get; }
    public ImmutableArray<FlyMapRegion> Regions { get; }
    public ImmutableArray<FlyMapPositionOverride> PositionOverrides { get; }
    public ImmutableArray<int> FlyIconFrameTicks { get; }
    public ImmutableDictionary<string, string> UiTexts { get; }
    public bool SwitchAvailable => false;
    public string GeneratedDirectory { get; }
    public ImmutableArray<FlyMapBackground> Backgrounds { get; }

    private PretFlyMap(string directory, JsonElement doc)
    {
        GeneratedDirectory = directory;
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive = false,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectNullableAnnotations = true,RespectRequiredConstructorParameters = true
        };
        T Parse<T>(string name) => JsonSerializer.Deserialize<T>(doc.GetProperty(name).GetRawText(), options)
            ?? throw new InvalidDataException("Null Fly map member: " + name);
        Need(doc.GetProperty("schemaVersion").GetInt32() == 1 &&
            !doc.GetProperty("switchAvailable").GetBoolean(), "Fly map document profile differs.");
        Backgrounds = Parse<ImmutableArray<FlyMapBackground>>("backgrounds");
        Assets = Parse<ImmutableArray<TownMapAsset>>("assets"); Regions = Parse<ImmutableArray<FlyMapRegion>>("regions");
        sections = Parse<ImmutableArray<FlyMapSection>>("sections").ToImmutableDictionary(s => s.SectionId, StringComparer.Ordinal);
        PositionOverrides = Parse<ImmutableArray<FlyMapPositionOverride>>("positionOverrides");
        FlyIconFrameTicks = Parse<ImmutableArray<int>>("flyIconFrameTicks");
        UiTexts = Parse<ImmutableDictionary<string, string>>("uiTexts");
        Need(Assets.Length == 2 && Backgrounds.Length == 12 && Assets.Select(a => a.Id).ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds) &&
            Regions.Length == 4 && sections.Count == 109 && PositionOverrides.Length == 27 && FlyIconFrameTicks.SequenceEqual([30, 60]),
            "Fly map inventory differs.");
        foreach (var a in Assets)
            Need(a.Width > 0 && a.Height > 0 && a.Width * a.Height <= 38400, "Fly map asset metadata differs.");
        int[] dungeonCounts = [12, 2, 2, 4],destinationCounts = [13, 3, 2, 2];
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

    /// <summary>Loads map graphics and destination data.</summary>
    public static PretFlyMap Load(string projectRoot)
    {
        string generated = Path.Combine(Path.GetFullPath(projectRoot), "content/generated/fly-map");
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(generated, "fly-map.json")));
        return new(generated, document.RootElement);
    }

    public TownMapAsset Asset(string id) => Assets.Single(a => a.Id == id);
    public byte[] ReadAssetPng(string id) => File.ReadAllBytes(Path.Combine(GeneratedDirectory, Asset(id).TexturePath));
    public int? RegionForSection(string sectionId) => sections.TryGetValue(sectionId, out var section) ? section.Region : null;
    public FlyMapRegion Region(int region) => region is >= 0 and < 4 ? Regions[region] : throw new ArgumentOutOfRangeException(nameof(region));
    public TownMapCell CellAt(int region, int x, int y, IReadOnlySet<string> flags)
    {
        if (x is < 0 or >= 22 || y is < 0 or >= 15) throw new ArgumentOutOfRangeException(nameof(x));
        var cell = Region(region).Cells[y * 22 + x];
        // Preserve GetMapsecUnderCursor's original shared Navel flag check (including Birth).
        if (cell.SectionId is "MAPSEC_NAVEL_ROCK" or "MAPSEC_BIRTH_ISLAND" && !flags.Contains("FLAG_WORLD_MAP_NAVEL_ROCK_EXTERIOR"))
            cell = cell with { SectionId = "MAPSEC_NONE",Name = "" };
        if (cell.DungeonSectionId == "MAPSEC_CERULEAN_CAVE" && !flags.Contains("FLAG_SYS_CAN_LINK_WITH_RS"))
            cell = cell with { DungeonSectionId = "MAPSEC_NONE",DungeonName = "" };
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

    /// <summary>Select an imported background; no runtime pixel composition.</summary>
    public FlyMapBackground Background(int region, IReadOnlySet<string> flags, bool dark = false)
    {
        _ = Region(region);
        bool hidden = region == 2 && !flags.Contains("FLAG_WORLD_MAP_NAVEL_ROCK_EXTERIOR") ||
            region == 3 && !flags.Contains("FLAG_WORLD_MAP_BIRTH_ISLAND_EXTERIOR");
        return Backgrounds.Single(b => b.MapRegion == region && b.Hidden == hidden && b.Dark == dark);
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
    private static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

}

public sealed record FlyMapRegion(int Index, ImmutableArray<TownMapCell> Cells, ImmutableArray<TownMapIcon> DungeonIcons, ImmutableArray<TownMapIcon> DestinationIcons);
public sealed record FlyMapSection(string SectionId, int Region, int X, int Y, int Width, int Height);
public sealed record FlyMapPositionOverride(string SectionId, string? SourceMap, int X, int Y);

public sealed record FlyMapBackground(int MapRegion, bool Hidden, bool Dark, string TexturePath, ImageRegion Region);
