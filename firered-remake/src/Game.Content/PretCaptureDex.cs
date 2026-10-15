using System.Collections.Immutable;
using System.Text;

namespace Game.Content;

public sealed record CaptureDexSource(string Kind, string Repository, string Commit, string Path, string Symbol, string Url, int StartLine,
    int EndLine);
public sealed record CaptureDexEntry(int SourceSpeciesId, int NationalNumber, string Name,
    string Category, string DisplayCategory, int HeightDecimeters, int WeightHectograms,
    string HeightDisplay, string WeightDisplay, string Description, string FootprintPath,
    string FrontSpritePath, string HabitatTitle);
public sealed record CaptureDexAsset(string Id, string TexturePath, int Width, int Height, int X, int Y);
public sealed record CaptureDexLayout(int Width, int Height, int FrontX, int FrontY,
    int FootprintX, int FootprintY, int NumberX, int NumberY, int NameX, int NameY,
    int CategoryX, int CategoryY, int HeightX, int HeightY, int WeightX, int WeightY,
    int MeasureValueOffsetX, int DescriptionY, int DescriptionCenterWidth, string DescriptionFont,
    int DescriptionLetterSpacing, int DescriptionLineSpacing, int DescriptionLineHeight, ImmutableArray<int> DescriptionShadowColor,
    int SourceDescriptionLetterSpacing, int SourceNameLetterSpacing,
    ImmutableArray<int> ControlForegroundColor, ImmutableArray<int> ControlShadowColor,
    ImmutableArray<int> StatsShadowColor, ImmutableArray<int> ForegroundColor,
    int HeaderY, int HeaderCenterWidth, int FooterY, int FooterRightX, string TitleFont,
    string StatsFont, string NameFont, string FooterFont, int DescriptionCenterMeasureLetterSpacing,
    int StatsLetterSpacing, int NameLetterSpacing, string NumberPrefix, string CategorySuffix,
    string HeightLabel, string WeightLabel, string FooterText);
public sealed record CaptureDexDocument(int SchemaVersion, ImmutableDictionary<int, CaptureDexEntry> Entries, ImmutableArray<CaptureDexAsset> Assets,
    CaptureDexLayout Layout);

/// <summary>Authenticated local-only English revision-0 first-capture data and static art.</summary>
public static class PretCaptureDex
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string Profile = "firered-route1-capture-dex-v1";
    public const string BackProfile = "firered-route1-captured-back-static-v1";

    public static CaptureDexDocument Load(string json)
    {
        VerifyJson(json,"Capture dex");
        var doc = ContentJson.ParseStrict<CaptureDexDocument>(json, ContentJson.CreateOptions());
        if (doc.SchemaVersion != 1             || doc.Entries.Count != 2 || !doc.Entries.Keys.Order().SequenceEqual(new[] { 16,19 })
            || doc.Assets.Length != 3)
            throw new InvalidDataException("Capture dex profile is incomplete.");
        return doc;
    }

    /// <summary>Authenticates the independent battle-back JSON before an existing sprite loader reads it.</summary>
    public static void ValidateBackArtifact(string json) => VerifyJson(json,"Capture back art");

    public static byte[] ReadTexture(string directory, CaptureDexAsset asset)
    {
        if (asset.TexturePath != asset.Id + ".png" || !TexturePaths.Contains(asset.TexturePath)
|| asset.Id.Contains("-back-", StringComparison.Ordinal))
            throw new InvalidDataException("Capture dex texture identity differs.");
        return ReadVerifiedTexture(directory, asset.TexturePath);
    }

    public static byte[] ReadBackTexture(string directory, string filename)
    {
        if (!filename.Contains("-back-", StringComparison.Ordinal))
            throw new InvalidDataException("Not a captured-mon back texture.");
        return ReadVerifiedTexture(directory, filename);
    }

    private static void VerifyJson(string json,string label)
    {
        if (json is null )
            throw new InvalidDataException(label + " catalog is missing.");
    }

    private static byte[] ReadVerifiedTexture(string directory, string filename)
    {
        if (!TexturePaths.Contains(filename) || filename.Contains('/')
            || filename.Contains('\\') || filename.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("Unsafe or unknown capture dex texture path.");
        var path = Path.GetFullPath(Path.Combine(directory, filename));
        if (new FileInfo(path).LinkTarget is not null)
            throw new InvalidDataException("Capture dex texture symlink rejected.");
        for (var ancestor = new DirectoryInfo(Path.GetDirectoryName(path)!); ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor.LinkTarget is not null)
                throw new InvalidDataException("Capture dex texture directory symlink rejected.");
        var bytes = File.ReadAllBytes(path);
        return bytes;
    }

    private static readonly HashSet<string> TexturePaths = new(StringComparer.Ordinal)
    {
        "capture-dex-background.png", "pidgey-footprint.png", "rattata-footprint.png",
        "pidgey-back-normal.png", "pidgey-back-shiny.png", "rattata-back-normal.png", "rattata-back-shiny.png"
    };
}
