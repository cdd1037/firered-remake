using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record VermilionGymLocalInput(string Path);
public sealed record VermilionGymPortraitSource(string Id, int Width, int Height, int X, int Y, int SourceTrainerPicId, int SourceFrame,
    string SourceImage, string SourcePalette);
public sealed record PretVermilionGymAssetsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<VermilionGymLocalInput> LocalInputs,
    PewterAdditionalMachine Tm34,
    PretPewterMachineIcon Icon, ImmutableArray<VermilionGymPortraitSource> Portraits,
    ImmutableArray<string> Limitations);

/// <summary>Original TM34, Engineer/Surge portraits and the bounded four-cue synthesis increment.</summary>
public sealed class PretVermilionGymAssets
{
    public const string Profile = "firered-vermilion-gym-assets-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const int SynthManifestBytes = 87317;
    public const int SynthCueCount = 243;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly ImmutableDictionary<string, byte[]> _outputs;
    public PretVermilionGymAssetsDocument Document { get; }
    public ImmutableArray<ForestTrainerPortrait> Portraits { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }

    private PretVermilionGymAssets(PretVermilionGymAssetsDocument document,
        ImmutableArray<PretWorldSourceFile> sourceFiles, ImmutableDictionary<string, byte[]> outputs)
    {
        Document = document; SourceFiles = sourceFiles; _outputs = outputs;
        Portraits = document.Portraits.Select(p => new ForestTrainerPortrait(p.Id, p.X, p.Y,
            outputs[p.Id + ".png"])).ToImmutableArray();
    }

    public byte[] ReadPng(string id) => ReadAsset(id);

    public byte[] ReadSynthManifestCandidate() => (byte[])_outputs["synth-manifest-candidate.json"].Clone();
    private byte[] ReadAsset(string id) => _outputs.TryGetValue(id + ".png", out var bytes)
        ? (byte[])bytes.Clone() : throw new InvalidDataException("Unknown Gym asset: " + id);

    public static PretVermilionGymAssetsDocument LoadDocument(string json, SharedMachineCatalog machines)
    {
        var document = ContentJson.ParseStrict<PretVermilionGymAssetsDocument>(json, machines.CreateJsonOptions());
        Require(document.SchemaVersion == 1 && document.Portraits.Length == 2 &&
            document.Tm34.Compatibility.Length == 73 && !document.Limitations.IsDefaultOrEmpty, "Gym asset scope differs.");
        var inputs = document.Inputs.ToDictionary(i => i.Path, StringComparer.Ordinal);
        Require(document.Tm34.Machine.Item.Id == PewterMachineRules.Tm34Id &&
            document.Tm34.Move == PewterMachineRules.ShockWaveId && document.Tm34.SourceMoveId == 351 &&
            document.Tm34.MovePp == 20 && document.Icon.Item == PewterMachineRules.Tm34Id &&
            document.Icon.Region is not null, "Gym TM34 identity differs.");
        PretPewterMachines.ValidateIcon(document.Icon);
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
        using var manifest = JsonDocument.Parse(Read(root,"content/generated/vermilion-gym-assets/manifest.json"));
var generated = manifest.RootElement;
        var outputs = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in generated.GetProperty("outputs").EnumerateArray())
        {
            string path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, Read(root,path.StartsWith("content/generated/", StringComparison.Ordinal) ? path : "content/generated/vermilion-gym-assets/" + path)), "Duplicate Gym output.");
        }
        var document = LoadDocument(Utf8.GetString(outputs["assets.json"]), SharedMachineCatalog.LoadProject(root));
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var input in document.Inputs)
        {
            files.Add(new(input.Path));
        }
        _ = PretPewterMachines.TexturePage(document.Icon, outputs[document.Icon.PngPath]);
        foreach (var portrait in document.Portraits)
            Require((outputs[portrait.Id + ".png"]).Length > 0, "Gym portrait pixels differ.");
        Require(outputs["synth-manifest-candidate.json"].Length == SynthManifestBytes , "Gym synth candidate differs.");
        return new(document, files.ToImmutable(), outputs.ToImmutable());
    }

    public PewterMachineCatalog ExtendMachines(PewterMachineCatalog old)
    {
        PewterMachineRules.ValidateCatalog(old);
        for (int n = 0; n < 73; n++)
        {
            var prior = old.Compatibility[n]; var current = Document.Tm34.Compatibility[n];
            Require(prior.Species == current.Species && prior.SourceSpeciesId == current.SourceSpeciesId,
                "Gym TM34 changed species order.");
        }
        var result = old with { Profile = PewterMachineRules.VermilionGymProfile,AdditionalMachines = old.AdditionalMachines!.Value.Add(Document.Tm34) };
        PewterMachineRules.ValidateCatalog(result);
        return result;
    }

    public static PewterMachineCatalog LoadCatalogProject(string root) => Load(root).ExtendMachines(PretSsAnneMachines.LoadCatalogProject(root));

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
