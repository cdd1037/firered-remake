using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>Authenticated two-page first-Sevii atlas extension. It grants no travel or unlock flags.</summary>
public sealed class PretFirstSeviiTownMap
{
    public const string Profile = "firered-first-sevii-town-map-v1";
    public const string SourceCommit = PretTownMap.SourceCommit;
    public const string UnlockFlag = "FLAG_SYS_SEVII_MAP_123";
    private static readonly string[] AssetIds = ["kanto-switch", "sevii123", "sevii123-locked", "switch-menu", "switch-left-0", "switch-left-1", "switch-right-0", "switch-right-1", "keypad-updown"];
    private readonly ImmutableDictionary<string, byte[]> outputs;
    private readonly ImmutableDictionary<string, FirstSeviiTownMapPosition> positionsByMap;
    public ImmutableArray<TownMapAsset> Assets { get; }
    public ImmutableArray<TownMapCell> Cells { get; }
    public ImmutableArray<TownMapIcon> Icons { get; }
    public ImmutableArray<FirstSeviiTownMapPosition> Positions { get; }
    public FirstSeviiTownMapSwitchMenu SwitchMenu { get; }

    private PretFirstSeviiTownMap(JsonElement d, ImmutableDictionary<string, byte[]> payloads)
    {
        T Parse<T>(string property) => ContentJson.ParseStrict<T>(d.GetProperty(property).GetRawText(), ContentJson.CreateOptions());
        outputs = payloads;
        Assets = Parse<ImmutableArray<TownMapAsset>>("assets"); Cells = Parse<ImmutableArray<TownMapCell>>("cells");
        Icons = Parse<ImmutableArray<TownMapIcon>>("icons"); Positions = Parse<ImmutableArray<FirstSeviiTownMapPosition>>("positions");
        SwitchMenu = Parse<FirstSeviiTownMapSwitchMenu>("switchMenu");
        positionsByMap = Positions.ToImmutableDictionary(p => p.StableMapId, StringComparer.Ordinal);
        Need(d.GetProperty("unlockFlag").GetString() == UnlockFlag,
            "First-Sevii Town Map document profile differs.");
        Need(Assets.Length == 9 && Assets.Select(a => a.Id).ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds), "First-Sevii atlas inventory differs.");
        foreach (var a in Assets)
            Need(a.Width > 0 && a.Height > 0 && a.Width * a.Height <= 38400 && (outputs[a.TexturePath]).Length > 0, "First-Sevii atlas asset differs.");
        Need(Cells.Length == 330 && Icons.Length == 2 && Positions.Length == 16 &&
            SwitchMenu == new FirstSeviiTownMapSwitchMenu(1, 6, 72, 168, 32, 88, 152, 32, 20, true), "First-Sevii atlas geometry differs.");
        for (int i = 0; i < Cells.Length; i++) Need(Cells[i].X == i % 22 && Cells[i].Y == i / 22, "First-Sevii atlas cell order differs.");
        foreach (var p in Positions)
            Need(p.Width > 0 && p.Height > 0 && p.SectionWidth > 0 && p.SectionHeight > 0 && p.Region is 0 or 1, "First-Sevii atlas position differs.");
        Need(Positions.Count(p => p.RequiresEscapeWarp) == 1 && positionsByMap["map.three-island-harbor"].RequiresEscapeWarp,
            "First-Sevii indoor escape-warp boundary differs.");
    }

    public static PretFirstSeviiTownMap Load(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);         var generated = Path.Combine(root, "content/generated/ui/first-sevii-town-map");
        byte[] Pinned(string directory,string relative)
        {
            var path = Safe(directory, relative); var info = new FileInfo(path);
            Need(info.Exists, "First-Sevii atlas file size differs: " + relative);
var bytes = File.ReadAllBytes(path);
return bytes;
        }
        using var manifest = JsonDocument.Parse(Pinned(generated,"manifest.json"));
        var m = manifest.RootElement;
        Need(m.GetProperty("schemaVersion").GetInt32() == 1, "First-Sevii atlas manifest differs.");

        var payloads = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var o in m.GetProperty("outputs").EnumerateArray())
        {
            var p = o.GetProperty("path").GetString()!;
            Need(payloads.TryAdd(p, Pinned(generated,p)), "Duplicate atlas output.");
        }
        Need(payloads.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(AssetIds.Select(id => id + ".png").Append("town-map.json")) , "First-Sevii atlas output inventory differs.");
        using var document = JsonDocument.Parse(payloads["town-map.json"]);
        return new(document.RootElement, payloads.ToImmutable());
    }

    public TownMapAsset Asset(string id) => Assets.Single(a => a.Id == id);
    public byte[] ReadAssetPng(string id) => outputs[Asset(id).TexturePath];
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
    TownMapPoint? Override, int Region, bool RequiresEscapeWarp);
public sealed record FirstSeviiTownMapSwitchMenu(int MaxSelection, int YOffset, int HighlightLeft, int HighlightRight,
    int HighlightHeight, int LeftArrowX, int RightArrowX, int ArrowSize, int ArrowFrameTicks, bool CancelRestoresPlayerRegion);
