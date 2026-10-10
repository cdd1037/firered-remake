using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretRoute11MachinesDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PewterAdditionalMachine> Machines, ImmutableArray<PretPewterMachineIcon> Icons, ImmutableArray<string> Limitations);

/// <summary>Authenticates thirteen explicit TM/HMs for 79 species, preserving every historical machine profile.</summary>
public static class PretRoute11Machines
{
    public const string Profile = PewterMachineRules.Route11Profile;
    public const string LockSha256 = "0080935d25724742161f9a29d71b76a8038082f3875661e7ef9f445f921358cc";
    public const string GeneratedSha256 = "ea2ea8d1ccc07063cdd2a6e5041686fc345759523cec120cd158638a07775ffd";
    public const string ManifestSha256 = "245b1d8a2b339e6b9e661c5252612c06ecf94a12d57713181cc83b41e788f319";
    public const string RawManifestSha256 = "2f98a9763c38e82bf0116a28c611ad5a6fcec15018a70423b84f4b0e66432046";
    public const string FullSourceHash = "ef8ee5721f035a107b04309bf7b20d92fd3866ebef194f52854488ac12b4e20e";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretRoute11MachinesDocument Load(string json)
    {
        Require(Hash(Utf8.GetBytes(json)) == GeneratedSha256, "Route11 machine document differs.");
        var document = ContentJson.ParseStrict<PretRoute11MachinesDocument>(json, ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.Profile == Profile &&
            document.SourceCommit == OpeningInventoryRules.SourceCommit && document.Inputs.Length == 29 &&
            document.Machines.Length == 13 && document.Icons.Length == 1 && !document.RulesSources.IsDefaultOrEmpty && !document.Limitations.IsDefaultOrEmpty,
            "Route11 machine scope differs.");
        var inputs = document.Inputs.ToDictionary(input => input.Path, StringComparer.Ordinal);
        Require(Hash(Utf8.GetBytes(string.Concat(document.Inputs.OrderBy(input => input.Path, StringComparer.Ordinal)
            .Select(input => input.Path + "\0" + input.Sha256 + "\n")))) == FullSourceHash,
            "Route11 machine input identity differs.");
        void Source(OpeningItemSourceRef source) => Require(source is not null &&
            !string.IsNullOrWhiteSpace(source.Symbol) && source.StartLine > 0 && source.EndLine >= source.StartLine &&
            inputs.TryGetValue(source.Path, out var input) && input.Sha256 == source.Sha256,
            "Route11 machine source reference differs.");
        foreach (var source in document.RulesSources) Source(source);
        string[] symbols = ["TM39", "TM05", "TM09", "TM46", "TM03", "TM45", "TM43", "TM28", "TM31", "TM44", "HM01", "TM34", "HM05"];
        for (int n = 0; n < document.Machines.Length; n++)
        {
            var machine = document.Machines[n];
            Require(machine.Machine.Item.Id.Value == "pret:ITEM_" + symbols[n] && machine.Compatibility.Length == 79,
                "Route11 machine order or compatibility differs.");
            Source(machine.Machine.Item.Source); Source(machine.Machine.DescriptionSource);
            foreach (var compatibility in machine.Compatibility) Source(compatibility.Source);
        }
        for (int n = 0; n < document.Icons.Length; n++)
        {
            var icon = document.Icons[n];
            Source(icon.Source); Source(icon.PaletteSource); Source(icon.BindingSource);
            Require(icon.Item.Value == "pret:ITEM_" + "HM05" &&
                icon.PngPath == "item_" + "HM05".ToLowerInvariant() + ".png",
                "Route11 machine icon identity differs.");
            _ = PretPewterMachines.IconRgba(icon);
        }
        return document;
    }

    public static PretRoute11MachinesDocument LoadProject(string root)
    {
        _ = Read(root, "scripts/route11-machines.lock.json", LockSha256);
        _ = Read(root, "content/raw/route11-machines/manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(Read(root, "content/generated/route11-machines/manifest.json", ManifestSha256));
        foreach (var output in manifest.RootElement.GetProperty("outputs").EnumerateArray())
            _ = Read(root, "content/generated/route11-machines/" + output.GetProperty("path").GetString(),
                output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32());
        var document = Load(Utf8.GetString(Read(root,
            "content/generated/route11-machines/route11-machines.json", GeneratedSha256)));
        foreach (var input in document.Inputs)
            Input(root, "content/raw/route11-machines/", input.Path, input.ByteLength, input.GitBlobSha1, input.Sha256);
        return document;
    }

    public static PewterMachineCatalog LoadCatalogProject(string root)
    {
        var document = LoadProject(root);
        var old = PretVermilionGymAssets.LoadCatalogProject(root);
        var legacy = new[] { PewterMachineRules.Definition(old, PewterMachineRules.Tm39Id)! }
            .Concat(old.AdditionalMachines!.Value).ToArray();
        for (int n = 0; n < legacy.Length; n++)
        {
            var prior = legacy[n]; var current = document.Machines[n];
            Require(prior.Machine == current.Machine && prior.Move == current.Move &&
                prior.SourceMoveId == current.SourceMoveId && prior.MovePp == current.MovePp &&
                prior.Compatibility.SequenceEqual(current.Compatibility.Take(73)),
                "Route11 changed historical machine metadata or compatibility.");
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
        Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob, "Route11 machine Git blob differs: " + path);
    }
    private static byte[] Read(string root, string path, string hash, int? length = null)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes && (length is null || info.Length == length), "Route11 machine file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        Require(Hash(bytes) == hash, "Route11 machine file differs: " + path);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Route11 machine path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("Route11 machine directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Route11 machine symlink rejected.");
        }
        return result;
    }
    private static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
