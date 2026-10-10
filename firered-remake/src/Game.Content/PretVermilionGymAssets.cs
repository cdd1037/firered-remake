using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record VermilionGymLocalInput(string Path, int ByteLength, string Sha256);
public sealed record VermilionGymPortraitSource(string Id, int Width, int Height, int X, int Y,
    int SourceTrainerPicId, int SourceFrame, string SourceImage, string SourcePalette,
    string RgbaSha256, string PngSha256);
public sealed record PretVermilionGymAssetsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<VermilionGymLocalInput> LocalInputs,
    ImmutableArray<OpeningItemSourceRef> RulesSources, PewterAdditionalMachine Tm34,
    PretPewterMachineIcon Icon, ImmutableArray<VermilionGymPortraitSource> Portraits,
    ImmutableArray<string> Limitations);

/// <summary>Original TM34, Engineer/Surge portraits and the bounded four-cue synthesis increment.</summary>
public sealed class PretVermilionGymAssets
{
    public const string Profile = "firered-vermilion-gym-assets-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string LockSha256 = "a6007bb7e7cb658fbb567c758bf38026aa5c2b94d875231738b5a7525cc00437";
    public const string ManifestSha256 = "8e903d87b0a0a7c44fa4f5ab7d3b1e4c09f92f2ab1b6d1828b2dbeab5c2f329a";
    public const string GeneratedSha256 = "327823b39dd7ed6b50762c1fec55b4bbf9f5289bd58546661d7051c89ba96860";
    public const string RawManifestSha256 = "27cf6ee70f3c7637e195cafe26c5cc358639cf3ddc500fd4ce15d2610960df8d";
    public const string SynthManifestSha256 = "fe4aedaa4a2a86a4cd78d14c182ab2d764cea5be66acb83deec131e86f79c3e6";
    public const int SynthManifestBytes = 87317;
    public const int SynthCueCount = 243;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly ImmutableDictionary<string, byte[]> _outputs;
    public PretVermilionGymAssetsDocument Document { get; }
    public ImmutableArray<ForestTrainerPortrait> Portraits { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }

    private PretVermilionGymAssets(PretVermilionGymAssetsDocument document,
        ImmutableArray<PretWorldSourceFile> sourceFiles, ImmutableDictionary<string, byte[]> outputs)
    {
        Document = document; SourceFiles = sourceFiles; _outputs = outputs;
        Portraits = document.Portraits.Select(p => new ForestTrainerPortrait(p.Id, p.X, p.Y,
            (byte[])outputs[p.Id + ".rgba"].Clone())).ToImmutableArray();
        AdditionalSourceHashes = ["vermilion-gym-assets-manifest:" + ManifestSha256,
            "vermilion-gym-assets-lock:" + LockSha256,
            .. sourceFiles.Select(f => "vermilion-gym-assets:" + f.Path + ":" + f.Sha256),
            .. document.LocalInputs.Select(f => "vermilion-gym-assets-dependency:" + f.Path + ":" + f.Sha256)];
    }

    public byte[] ReadPng(string id) => ReadAsset(id, ".png");
    public byte[] ReadRgba(string id) => ReadAsset(id, ".rgba");
    public byte[] ReadSynthManifestCandidate() => (byte[])_outputs["synth-manifest-candidate.json"].Clone();
    private byte[] ReadAsset(string id, string extension) => _outputs.TryGetValue(id + extension, out var bytes)
        ? (byte[])bytes.Clone() : throw new InvalidDataException("Unknown Gym asset: " + id);

    public static PretVermilionGymAssetsDocument LoadDocument(string json)
    {
        Require(Hash(Utf8.GetBytes(json)) == GeneratedSha256, "Gym asset document differs.");
        var document = ContentJson.ParseStrict<PretVermilionGymAssetsDocument>(json, ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.Profile == Profile && document.SourceCommit == SourceCommit &&
            document.Inputs.Length == 36 && document.LocalInputs.Length == 4 && document.Portraits.Length == 2 &&
            document.Tm34.Compatibility.Length == 73 && !document.RulesSources.IsDefaultOrEmpty &&
            !document.Limitations.IsDefaultOrEmpty, "Gym asset scope differs.");
        var inputs = document.Inputs.ToDictionary(i => i.Path, StringComparer.Ordinal);
        void Source(OpeningItemSourceRef source) => Require(source is not null &&
            !string.IsNullOrWhiteSpace(source.Symbol) && source.StartLine > 0 && source.EndLine >= source.StartLine &&
            inputs.TryGetValue(source.Path, out var input) && input.Sha256 == source.Sha256,
            "Gym source reference differs.");
        foreach (var source in document.RulesSources) Source(source);
        Source(document.Tm34.Machine.Item.Source); Source(document.Tm34.Machine.DescriptionSource);
        foreach (var row in document.Tm34.Compatibility) Source(row.Source);
        Source(document.Icon.Source); Source(document.Icon.PaletteSource); Source(document.Icon.BindingSource);
        Require(document.Tm34.Machine.Item.Id == PewterMachineRules.Tm34Id &&
            document.Tm34.Move == PewterMachineRules.ShockWaveId && document.Tm34.SourceMoveId == 351 &&
            document.Tm34.MovePp == 20 && document.Icon.Item == PewterMachineRules.Tm34Id &&
            document.Icon.PngPath == "item_tm34.png", "Gym TM34 identity differs.");
        _ = PretPewterMachines.IconRgba(document.Icon);
        for (int n = 0; n < 2; n++)
        {
            var portrait = document.Portraits[n];
            string stem = n == 0 ? "engineer" : "leader_lt_surge";
            Require(portrait.Id == (n == 0 ? "engineer-front" : "lt-surge-front") &&
                portrait.SourceTrainerPicId == (n == 0 ? 93 : 118) && portrait.SourceFrame == 0 &&
                portrait.Width == 64 && portrait.Height == 64 && portrait.X == 144 && portrait.Y == 8 &&
                portrait.SourceImage == "graphics/trainers/front_pics/" + stem + "_front_pic.png" &&
                portrait.SourcePalette == "graphics/trainers/palettes/" + stem + ".pal" &&
                inputs.ContainsKey(portrait.SourceImage) && inputs.ContainsKey(portrait.SourcePalette),
                "Gym original portrait geometry/binding differs.");
        }
        return document;
    }

    public static PretVermilionGymAssets Load(string root)
    {
        using var sourceLock = JsonDocument.Parse(Read(root, "scripts/vermilion-gym-assets.lock.json", LockSha256, 9529));
        _ = Read(root, "content/raw/vermilion-gym-assets/manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(Read(root, "content/generated/vermilion-gym-assets/manifest.json", ManifestSha256));
        var locked = sourceLock.RootElement; var generated = manifest.RootElement;
        Require(locked.GetProperty("repository").GetString() == "pret/pokefirered" &&
            locked.GetProperty("commit").GetString() == SourceCommit && locked.GetProperty("tree").GetString() == SourceTree &&
            generated.GetProperty("profile").GetString() == Profile && generated.GetProperty("sourceCommit").GetString() == SourceCommit &&
            generated.GetProperty("sourceTree").GetString() == SourceTree && generated.GetProperty("lockSha256").GetString() == LockSha256,
            "Gym locked source identity differs.");
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in generated.GetProperty("outputs").EnumerateArray())
        {
            string path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, Read(root, "content/generated/vermilion-gym-assets/" + path,
                output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32())), "Duplicate Gym output.");
        }
        Require(outputs.Count == 10, "Gym output inventory differs.");
        var document = LoadDocument(Utf8.GetString(outputs["assets.json"]));
        using var documentJson = JsonDocument.Parse(outputs["assets.json"]);
        Require(JsonElement.DeepEquals(documentJson.RootElement.GetProperty("inputs"), locked.GetProperty("files")) &&
            JsonElement.DeepEquals(documentJson.RootElement.GetProperty("localInputs"), locked.GetProperty("inputs")),
            "Gym source/dependency inventory differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var input in document.Inputs)
        {
            var bytes = Read(root, "content/raw/vermilion-gym-assets/" + input.Path, input.Sha256, input.ByteLength);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) == input.GitBlobSha1,
                "Gym original Git blob differs: " + input.Path);
            files.Add(new(input.Path, input.GitBlobSha1, input.ByteLength, input.Sha256,
                $"https://github.com/pret/pokefirered/blob/{SourceCommit}/{input.Path}"));
        }
        foreach (var input in document.LocalInputs) _ = Read(root, input.Path, input.Sha256, input.ByteLength);
        Require(outputs["item_tm34.rgba"].SequenceEqual(PretPewterMachines.IconRgba(document.Icon)), "Gym icon expanded pixels differ.");
        foreach (var portrait in document.Portraits)
            Require(outputs[portrait.Id + ".rgba"].Length == 64 * 64 * 4 &&
                Hash(outputs[portrait.Id + ".rgba"]) == portrait.RgbaSha256 &&
                Hash(outputs[portrait.Id + ".png"]) == portrait.PngSha256, "Gym portrait pixels differ.");
        Require(outputs["synth-manifest-candidate.json"].Length == SynthManifestBytes &&
            Hash(outputs["synth-manifest-candidate.json"]) == SynthManifestSha256, "Gym synth candidate differs.");
        return new(document, files.ToImmutable(), outputs.ToImmutable());
    }

    public PewterMachineCatalog ExtendMachines(PewterMachineCatalog old)
    {
        Require(old.Profile == PewterMachineRules.SsAnneProfile, "Gym machines require the unchanged ship profile.");
        PewterMachineRules.ValidateCatalog(old);
        for (int n = 0; n < 73; n++)
        {
            var prior = old.Compatibility[n]; var current = Document.Tm34.Compatibility[n];
            Require(prior.Species == current.Species && prior.SourceSpeciesId == current.SourceSpeciesId && prior.Source == current.Source,
                "Gym TM34 changed historical species/source order.");
        }
        var result = old with { Profile = PewterMachineRules.VermilionGymProfile,
            AdditionalMachines = old.AdditionalMachines!.Value.Add(Document.Tm34) };
        PewterMachineRules.ValidateCatalog(result);
        return result;
    }

    public static PewterMachineCatalog LoadCatalogProject(string root) => Load(root).ExtendMachines(PretSsAnneMachines.LoadCatalogProject(root));

    private static byte[] Read(string root, string relative, string hash, int? length = null)
    {
        Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) && !relative.Contains('\\') &&
            !relative.Split('/').Any(p => p is "" or "." or "..") && !root.Split(Path.DirectorySeparatorChar).Contains(".."), "Unsafe Gym asset path.");
        var info = new FileInfo(Path.Combine(Path.GetFullPath(root), relative));
        Require(info.LinkTarget is null, "Gym asset symlink rejected.");
        for (var parent = info.Directory; parent is not null; parent = parent.Parent)
            Require(parent.LinkTarget is null, "Gym asset directory symlink rejected.");
        using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        Require(stream.Length is > 0 and <= 3_000_000 && (length is null || stream.Length == length), "Gym asset length differs: " + relative);
        byte[] bytes = new byte[stream.Length]; stream.ReadExactly(bytes);
        Require(stream.ReadByte() == -1 && Hash(bytes) == hash, "Gym asset hash differs: " + relative);
        return bytes;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
