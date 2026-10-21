using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record VermilionGymPortraitSource(string Id, int Width, int Height, int X, int Y, int SourceTrainerPicId, int SourceFrame,
    string SourceImage, string SourcePalette);
public sealed record PretVermilionGymAssetsDocument(int SchemaVersion,
    ItemIcon Icon, ImmutableArray<VermilionGymPortraitSource> Portraits);

/// <summary>TM34 icon and Engineer/Surge portraits.</summary>
public sealed class PretVermilionGymAssets
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PretVermilionGymAssetsDocument Document { get; }
    public ImmutableArray<ForestTrainerPortrait> Portraits { get; }

    private PretVermilionGymAssets(PretVermilionGymAssetsDocument document,
        ImmutableDictionary<string, byte[]> outputs)
    {
        Document = document;
        Portraits = document.Portraits.Select(p => new ForestTrainerPortrait(p.Id, p.X, p.Y,
            outputs[p.Id + ".png"])).ToImmutableArray();
    }

    public static PretVermilionGymAssetsDocument LoadDocument(string json)
    {
        var document = ContentJson.ParseStrict<PretVermilionGymAssetsDocument>(json, ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.Portraits.Length == 2, "Gym asset scope differs.");
        Require(document.Icon.Item == MachineRules.Tm34Id &&
            document.Icon.Region is not null, "Gym TM34 identity differs.");
        ItemIconAssets.Validate(document.Icon);
        for (int n = 0; n < 2; n++)
        {
            var portrait = document.Portraits[n];
            string stem = n == 0 ? "engineer" : "leader_lt_surge";
            Require(portrait.Id == (n == 0 ? "engineer-front" : "lt-surge-front") &&
                portrait.SourceTrainerPicId == (n == 0 ? 93 : 118) && portrait.SourceFrame == 0 &&
                portrait.Width == 64 && portrait.Height == 64 && portrait.X == 144 && portrait.Y == 8 &&
                portrait.SourceImage == "graphics/trainers/front_pics/" + stem + "_front_pic.png" &&
                portrait.SourcePalette == "graphics/trainers/palettes/" + stem + ".pal",
                "Gym original portrait geometry/binding differs.");
        }
        return document;
    }

    public static PretVermilionGymAssets Load(string root)
    {
        using var manifest = JsonDocument.Parse(Read(root,"content/generated/ui/vermilion-gym-assets/manifest.json"));
var generated = manifest.RootElement;
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in generated.GetProperty("outputs").EnumerateArray())
        {
            string path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, Read(root,path.StartsWith("content/generated/", StringComparison.Ordinal) ? path : "content/generated/ui/vermilion-gym-assets/" + path)), "Duplicate Gym output.");
        }
        var document = LoadDocument(Utf8.GetString(outputs["assets.json"]));
        Require(outputs.ContainsKey(document.Icon.PngPath), "Gym item texture page is missing.");
        foreach (var portrait in document.Portraits)
            Require((outputs[portrait.Id + ".png"]).Length > 0, "Gym portrait pixels differ.");
        return new(document, outputs.ToImmutable());
    }

    private static byte[] Read(string root,string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\') &&
            !relative.Split('/').Any(p => p is "" or "." or "..") && !root.Split(Path.DirectorySeparatorChar).Contains(".."), "Unsafe Gym asset path.");
        var info = new FileInfo(Path.Combine(Path.GetFullPath(root), relative));
        Require(info.LinkTarget is null, "Gym asset symlink rejected.");
        for (var parent = info.Directory; parent is not null; parent = parent.Parent)
            Require(parent.LinkTarget is null, "Gym asset directory symlink rejected.");
var bytes = File.ReadAllBytes(info.FullName);
return bytes;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
