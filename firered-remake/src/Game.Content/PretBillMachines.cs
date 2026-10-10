using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretBillMachinesDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PewterAdditionalMachine> Machines, ImmutableArray<PretPewterMachineIcon> Icons, ImmutableArray<string> Limitations);

/// <summary>Authenticates eight explicit TMs for 68 species, preserving every historical machine profile.</summary>
public static class PretBillMachines
{
    public const string Profile = PewterMachineRules.BillProfile;
    public const string LockSha256 = "f6ae07d1526476c3e280103d3fec8c0810f8a4211636d616e2055df86bb80e28";
    public const string GeneratedSha256 = "b74b8db014f6ad44abffd1966559fe15916767e9beb841b2854e38b539560d63";
    public const string ManifestSha256 = "69804d2ccfe963e779a044017b6cfc45209a528f52bd059e957810e84b438978";
    public const string RawManifestSha256 = "ac3070048ca6ccab07e3725eb92e5886b4f3d8eac999349f96636465f2f73c17";
    public const string FullSourceHash = "ef28fd3a1b871d19fd9019206c5a34c836c34647cd7e07acd0dca35e4cb300d1";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretBillMachinesDocument Load(string json)
    {
        Require(Hash(Utf8.GetBytes(json)) == GeneratedSha256, "Bill machine document differs.");
        var document = ContentJson.ParseStrict<PretBillMachinesDocument>(json, ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.Profile == Profile &&
            document.SourceCommit == OpeningInventoryRules.SourceCommit && document.Inputs.Length == 25 &&
            document.Machines.Length == 8 && document.Icons.Length == 3 && !document.RulesSources.IsDefaultOrEmpty && !document.Limitations.IsDefaultOrEmpty,
            "Bill machine scope differs.");
        var inputs = document.Inputs.ToDictionary(input => input.Path, StringComparer.Ordinal);
        Require(Hash(Utf8.GetBytes(string.Concat(document.Inputs.OrderBy(input => input.Path, StringComparer.Ordinal)
            .Select(input => input.Path + "\0" + input.Sha256 + "\n")))) == FullSourceHash,
            "Bill machine input identity differs.");
        void Source(OpeningItemSourceRef source) => Require(source is not null &&
            !string.IsNullOrWhiteSpace(source.Symbol) && source.StartLine > 0 && source.EndLine >= source.StartLine &&
            inputs.TryGetValue(source.Path, out var input) && input.Sha256 == source.Sha256,
            "Bill machine source reference differs.");
        foreach (var source in document.RulesSources) Source(source);
        string[] symbols = ["TM39", "TM05", "TM09", "TM46", "TM03", "TM45", "TM43", "TM28"];
        for (int n = 0; n < document.Machines.Length; n++)
        {
            var machine = document.Machines[n];
            Require(machine.Machine.Item.Id.Value == "pret:ITEM_" + symbols[n] && machine.Compatibility.Length == 68,
                "Bill machine order or compatibility differs.");
            Source(machine.Machine.Item.Source); Source(machine.Machine.DescriptionSource);
            foreach (var compatibility in machine.Compatibility) Source(compatibility.Source);
        }
        for (int n = 0; n < document.Icons.Length; n++)
        {
            var icon = document.Icons[n];
            Source(icon.Source); Source(icon.PaletteSource); Source(icon.BindingSource);
            Require(icon.Item.Value == "pret:ITEM_" + symbols[n + 5] &&
                icon.PngPath == "item_" + symbols[n + 5].ToLowerInvariant() + ".png",
                "Bill machine icon identity differs.");
            _ = PretPewterMachines.IconRgba(icon);
        }
        return document;
    }

    public static PretBillMachinesDocument LoadProject(string root)
    {
        _ = Read(root, "scripts/bill-machines.lock.json", LockSha256);
        _ = Read(root, "content/raw/bill-machines/manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(Read(root, "content/generated/bill-machines/manifest.json", ManifestSha256));
        foreach (var output in manifest.RootElement.GetProperty("outputs").EnumerateArray())
            _ = Read(root, "content/generated/bill-machines/" + output.GetProperty("path").GetString(),
                output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32());
        var document = Load(Utf8.GetString(Read(root,
            "content/generated/bill-machines/bill-machines.json", GeneratedSha256)));
        foreach (var input in document.Inputs)
            Input(root, "content/raw/bill-machines/", input.Path, input.ByteLength, input.GitBlobSha1, input.Sha256);
        return document;
    }

    public static PewterMachineCatalog LoadCatalogProject(string root)
    {
        var document = LoadProject(root);
        var old = PretCeruleanMachines.LoadCatalogProject(root);
        var legacy = new[] { PewterMachineRules.Definition(old, PewterMachineRules.Tm39Id)! }
            .Concat(old.AdditionalMachines!.Value).ToArray();
        for (int n = 0; n < legacy.Length; n++)
        {
            var prior = legacy[n]; var current = document.Machines[n];
            Require(prior.Machine == current.Machine && prior.Move == current.Move &&
                prior.SourceMoveId == current.SourceMoveId && prior.MovePp == current.MovePp &&
                prior.Compatibility.SequenceEqual(current.Compatibility.Take(65)),
                "Bill changed historical machine metadata or compatibility.");
        }
        var result = old with
        {
            Profile = Profile,
            Compatibility = document.Machines[0].Compatibility,
            AdditionalMachines = document.Machines.Skip(1).ToImmutableArray()
        };
        PewterMachineRules.ValidateCatalog(result);
        return result;
    }

    private static void Input(string root, string prefix, string path, int length, string blob, string hash)
    {
        var bytes = Read(root, prefix + path, hash, length);
        Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob, "Bill machine Git blob differs: " + path);
    }
    private static byte[] Read(string root, string path, string hash, int? length = null)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes && (length is null || info.Length == length), "Bill machine file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        Require(Hash(bytes) == hash, "Bill machine file differs: " + path);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Bill machine path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("Bill machine directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Bill machine symlink rejected.");
        }
        return result;
    }
    private static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
