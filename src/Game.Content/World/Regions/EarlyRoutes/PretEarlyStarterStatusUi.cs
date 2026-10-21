using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record EarlyStarterStatusUiPoint(int X, int Y);
public sealed record EarlyStarterStatusUiAsset(string Id, string TexturePath, int Width, int Height, int X, int Y, string Context,
    string Status, int SourceTile, string SourceImagePath, string SourcePalettePath,
    ImmutableArray<int> PaletteRgb555, int TransparentIndex);
public sealed record EarlyStarterStatusUiDocument(int SchemaVersion, string Repository, string RawRoot,
    ImmutableArray<EarlyStarterStatusUiAsset> Assets,
    ImmutableArray<EarlyStarterStatusUiPoint> PartyStatusTopLeft);

/// <summary>Loads status textures and their display layout.</summary>
public static class PretEarlyStarterStatusUi
{
    public const string Profile = "firered-early-starter-status-ui-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    private const string Generated = "content/generated/ui/early-starter-status";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly ImmutableDictionary<string, (int Width, int X, int Y)> Expected =
        new Dictionary<string, (int, int, int)>(StringComparer.Ordinal)
        {
            ["battle-player-sleep"] = (24, 142, 96),
            ["battle-opponent-sleep"] = (24, 20, 30),
            ["party-sleep"] = (32, 0, 0)
        }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>Loads the status catalog and textures.</summary>
    public static EarlyStarterStatusUiDocument Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var generated = GeneratedResourceFile.SafePath(project, Generated);
        using var manifest = JsonDocument.Parse(ReadPinned(generated,"manifest.json"));
        var catalogBytes = ReadPinned(generated,"early-starter-status-ui.json");
        var doc = ContentJson.ParseStrict<EarlyStarterStatusUiDocument>(Utf8.GetString(catalogBytes), ContentJson.CreateOptions());
        Require(doc.SchemaVersion == 1 && doc.Assets.Length == 3
            && doc.Assets.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() == doc.Assets.Length,
            "Early-starter status catalog layout differs.");
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1, "Early-starter status manifest schema differs.");
        var outputs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.Add(path), "Duplicate early-starter status output.");
        }
        Require(outputs.SetEquals(doc.Assets.Select(a => a.TexturePath).Append("early-starter-status-ui.json")),
            "Early-starter status output inventory differs.");
        foreach (var asset in doc.Assets)
        {
            _ = ReadTexture(project, asset);
        }
        return doc;
    }

    /// <summary>Reads a generated PNG using the repository root.</summary>
    public static byte[] ReadTexture(string projectRoot, EarlyStarterStatusUiAsset asset)
    {
        ValidateAsset(asset);
        var bytes = ReadPinned(projectRoot,Generated + "/" + asset.TexturePath);
        return bytes;
    }

    /// <summary>Generated PNG bytes for the rendering layer.</summary>

    private static void ValidateAsset(EarlyStarterStatusUiAsset asset)
    {
        Require(asset is not null && Expected.TryGetValue(asset.Id, out var e)
            && asset.TexturePath == asset.Id + ".png"
            && (asset.Width, asset.Height, asset.X, asset.Y) == (e.Width, 8, e.X, e.Y)
            && asset.TransparentIndex == 0
            && asset.PaletteRgb555.Length == 16 && asset.PaletteRgb555.All(c => c is >= 0 and <= 32767)
            && asset.Status == "sleep" && asset.Context == asset.Id[..^6],
            "Early-starter status asset layout is invalid.");
    }

    private static byte[] ReadPinned(string root,string path)
    {
        var filename = GeneratedResourceFile.SafePath(root, path);
        var info = new FileInfo(filename);
        Require(info.Length is > 0 and <= 1_000_000 ,
            "Early-starter status file size differs: " + path);
        var bytes = File.ReadAllBytes(filename);
        return bytes;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
