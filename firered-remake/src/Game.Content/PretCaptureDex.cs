using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Game.Content;

public sealed record CaptureDexSource(string Kind, string Repository, string Commit, string Path,
    string Symbol, string Sha256, string Url, int StartLine, int EndLine);
public sealed record CaptureDexEntry(int SourceSpeciesId, int NationalNumber, string Name,
    string Category, string DisplayCategory, int HeightDecimeters, int WeightHectograms,
    string HeightDisplay, string WeightDisplay, string Description, string FootprintPath,
    string FrontSpritePath, string HabitatTitle, CaptureDexSource Source, CaptureDexSource DescriptionSource);
public sealed record CaptureDexAsset(string Id, string TexturePath, int Width, int Height,
    int X, int Y, string Sha256, string RgbaSha256);
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
public sealed record CaptureDexDocument(int SchemaVersion, string Profile, string SourceCommit,
    string FullSourceHash, ImmutableArray<PretPokemonInput> Inputs,
    ImmutableDictionary<int, CaptureDexEntry> Entries, ImmutableArray<CaptureDexAsset> Assets,
    CaptureDexLayout Layout, ImmutableArray<string> Limitations);

/// <summary>Authenticated local-only English revision-0 first-capture data and static art.</summary>
public static class PretCaptureDex
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string Profile = "firered-route1-capture-dex-v1";
    public const string BackProfile = "firered-route1-captured-back-static-v1";
    public const string GeneratedSha256 = "689d076758311629ab50a5e599345030aca63040e13c4956146a92cb6129bcd4";
    public const string BackGeneratedSha256 = "ed9bbd914b5e17afabcba225aa3aa876f34006f3b78e8622f36bf69310d008be";
    public const string SourceManifestSha256 = "0c095dbeff2ba1e298bc9cfae9f0134750bf7e73192c1a148f6c4bfa357ae1f3";

    public static CaptureDexDocument Load(string json)
    {
        VerifyJson(json, GeneratedSha256, "Capture dex");
        var doc = ContentJson.ParseStrict<CaptureDexDocument>(json, ContentJson.CreateOptions());
        if (doc.SchemaVersion != 1 || doc.Profile != Profile || doc.SourceCommit != SourceCommit
            || doc.FullSourceHash != SourceManifestSha256 || doc.Inputs.Length != 38
            || doc.Entries.Count != 2 || !doc.Entries.Keys.Order().SequenceEqual(new[] { 16, 19 })
            || doc.Assets.Length != 3 || doc.Limitations.IsDefaultOrEmpty)
            throw new InvalidDataException("Capture dex profile is incomplete.");
        return doc;
    }

    /// <summary>Authenticates the independent battle-back JSON before an existing sprite loader reads it.</summary>
    public static void ValidateBackArtifact(string json) => VerifyJson(json, BackGeneratedSha256, "Capture back art");

    public static byte[] ReadTexture(string directory, CaptureDexAsset asset)
    {
        if (asset.TexturePath != asset.Id + ".png" || !ExpectedTextures.TryGetValue(asset.TexturePath, out var expected)
            || asset.Sha256 != expected || asset.Id.Contains("-back-", StringComparison.Ordinal))
            throw new InvalidDataException("Capture dex texture identity differs.");
        return ReadVerifiedTexture(directory, asset.TexturePath);
    }

    public static byte[] ReadBackTexture(string directory, string filename)
    {
        if (!filename.Contains("-back-", StringComparison.Ordinal))
            throw new InvalidDataException("Not a captured-mon back texture.");
        return ReadVerifiedTexture(directory, filename);
    }

    private static void VerifyJson(string json, string expected, string label)
    {
        if (json is null || Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))) != expected)
            throw new InvalidDataException(label + " catalog hash differs; run the verified importer.");
    }

    private static byte[] ReadVerifiedTexture(string directory, string filename)
    {
        if (!ExpectedTextures.TryGetValue(filename, out var expected) || filename.Contains('/')
            || filename.Contains('\\') || filename.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("Unsafe or unknown capture dex texture path.");
        var path = Path.GetFullPath(Path.Combine(directory, filename));
        if (new FileInfo(path).LinkTarget is not null)
            throw new InvalidDataException("Capture dex texture symlink rejected.");
        for (var ancestor = new DirectoryInfo(Path.GetDirectoryName(path)!); ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor.LinkTarget is not null)
                throw new InvalidDataException("Capture dex texture directory symlink rejected.");
        var bytes = File.ReadAllBytes(path);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected)
            throw new InvalidDataException("Capture dex texture hash differs: " + filename);
        return bytes;
    }

    private static readonly IReadOnlyDictionary<string, string> ExpectedTextures =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["capture-dex-background.png"] = "580534eb2e35e17b05f7e6fb242be081a369ce5ea05ae4ac7ad1c659895a3906",
            ["pidgey-footprint.png"] = "1955a3f8570a4ffe00e20c7a385b69edb56e33ebd09ac258032577982e86f9e1",
            ["rattata-footprint.png"] = "0ef8c3424c7005a30158e38e1c9e21b7d1ee12d7b2f6599f42e973347bed2cb3",
            ["pidgey-back-normal.png"] = "b50bd4466f85293dd5f04165934acd6a510a4a417a326563cbb6cd00143fcf62",
            ["pidgey-back-shiny.png"] = "61f1a7721b1661eb32f6717092e8a7ce8ddb2492948c2dea823d22e6609ac90d",
            ["rattata-back-normal.png"] = "40dced3c07d411ef332efadb9c392d6f6ce456b7a60228b99b5fc9651d22dc9b",
            ["rattata-back-shiny.png"] = "79b2f18519a8cd7a39828789b241a2bda168066368b194b9eb396364f5ae121a",
        };
}
