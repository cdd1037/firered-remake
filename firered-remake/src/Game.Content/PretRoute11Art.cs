using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

public sealed record Route11StatusAsset(string Id, int Width, int Height, int X, int Y, byte[] Rgba);
public sealed record Route11ArtBundle(ImmutableArray<ForestTrainerPortrait> Portraits,
    ImmutableArray<Route11StatusAsset> StatusAssets);

/// <summary>GAMER portrait and freeze status pixels.</summary>
public static class PretRoute11Art
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string Profile = "firered-route11-art-v1";
    private const string Generated = "content/generated/route11-art/";
    private static readonly ImmutableDictionary<string, (int W, int H, int X, int Y)> Expected =
        new Dictionary<string, (int, int, int, int)>(StringComparer.Ordinal)
        {
            ["gamer-front"] = (64, 64, 144, 8),
            ["battle-player-freeze"] = (24, 8, 142, 96),
            ["battle-opponent-freeze"] = (24, 8, 20, 30),
            ["party-freeze"] = (32, 8, 0, 0)
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public static Route11ArtBundle Load(string root)
    {
        using var catalog = JsonDocument.Parse(Read(root, Generated + "route11-art.json"));
        var c = catalog.RootElement;
        Require(I(c, "schemaVersion") == 1, "Unsupported Route11 art schema.");
        var portraits = ImmutableArray.CreateBuilder<ForestTrainerPortrait>();
        var statuses = ImmutableArray.CreateBuilder<Route11StatusAsset>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in c.GetProperty("assets").EnumerateArray())
        {
            string id = S(asset, "id");
            Require(seen.Add(id) && Expected.ContainsKey(id), "Route11 asset identity differs.");
            var e = Expected[id];
            Require((I(asset, "width"), I(asset, "height"), I(asset, "x"), I(asset, "y")) == (e.W, e.H, e.X, e.Y),
                "Route11 asset layout differs.");
            var pixels = PngRgba8.Decode(Read(root, Generated + S(asset, "texturePath")), e.W, e.H);
            Require(pixels.Length == e.W * e.H * 4, "Route11 pixel buffer size differs.");
            Require(Enumerable.Range(0, pixels.Length / 4).All(i => pixels[4 * i + 3] is 0 or 255)
                && Enumerable.Range(0, pixels.Length / 4).Any(i => pixels[4 * i + 3] == 255), "Route11 pixel alpha differs.");
            if (id == "gamer-front")
            {
                Require(S(asset, "kind") == "trainer", "GAMER asset kind differs.");
                portraits.Add(new(id, e.X, e.Y, pixels));
            }
            else
            {
                Require(S(asset, "kind") == "status", "Freeze kind differs.");
                statuses.Add(new(id, e.W, e.H, e.X, e.Y, pixels));
            }
        }
        Require(portraits.Count == 1 && statuses.Count == 3 && seen.SetEquals(Expected.Keys), "Route11 art bundle incomplete.");
        return new(portraits.ToImmutable(), statuses.ToImmutable());
    }

    private static byte[] Read(string root,string relative)
    {
        Require(!string.IsNullOrWhiteSpace(root) && !root.Split(Path.DirectorySeparatorChar).Contains("..")
            && !string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\')
            && !relative.Split('/').Any(p => p is "" or "." or ".."), "Unsafe Route11 art path.");
        var directory = Path.GetFullPath(root); var path = Path.GetFullPath(Path.Combine(directory, relative));
        Require(path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Route11 art path escapes its root.");
        var file = new FileInfo(path); Require(file.LinkTarget is null, "Route11 art file symlink rejected.");
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
            Require(parent.LinkTarget is null, "Route11 art directory symlink rejected.");
        var bytes = File.ReadAllBytes(path);         return bytes;
    }

    private static string S(JsonElement value, string key) => value.GetProperty(key).GetString() ?? throw new InvalidDataException("Null Route11 art property.");
    private static int I(JsonElement value, string key) => value.GetProperty(key).GetInt32();
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
