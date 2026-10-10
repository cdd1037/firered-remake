using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretRockTunnelMachinesDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PewterAdditionalMachine> Machines, ImmutableArray<PretPewterMachineIcon> Icons, ImmutableArray<string> Limitations);

/// <summary>Authenticates fourteen explicit TM/HMs for 93 species, preserving every historical machine profile.</summary>
public static class PretRockTunnelMachines
{
    public const string Profile = RockTunnelMachineRules.Profile;
    public const string LockSha256 = "c99287370abebd3548605abec13caefaaa0d7678da5f6988612731fc34389616";
    public const string GeneratedSha256 = "287f9dd625a67b882d46df022d30cae269d7bea1b5d2867620f43f0d4afb9ecb";
    public const string ManifestSha256 = "a0079b7231bf86638f7f10aa62b157315fdaa1b815e4096bf5e73f924d9ce0d5";
    public const string RawManifestSha256 = "44eb4a61e9c067fbe47c1a51ad76caa66c26d234576954d51efa264ca1247ccf";
    public const string FullSourceHash = "075b625277306e4dc9aba7df78fd3087c98cdb8f2b927461abe5655300bf7e34";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretRockTunnelMachinesDocument Load(string json)
    {
        Require(Hash(Utf8.GetBytes(json)) == GeneratedSha256, "RockTunnel machine document differs.");
        var document = ContentJson.ParseStrict<PretRockTunnelMachinesDocument>(json, ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.Profile == Profile &&
            document.SourceCommit == OpeningInventoryRules.SourceCommit && document.Inputs.Length == 53 &&
            document.Machines.Length == 14 && document.Icons.Length == 1 && !document.RulesSources.IsDefaultOrEmpty && !document.Limitations.IsDefaultOrEmpty,
            "RockTunnel machine scope differs.");
        var inputs = document.Inputs.ToDictionary(input => input.Path, StringComparer.Ordinal);
        Require(Hash(Utf8.GetBytes(string.Concat(document.Inputs.OrderBy(input => input.Path, StringComparer.Ordinal)
            .Select(input => input.Path + "\0" + input.Sha256 + "\n")))) == FullSourceHash,
            "RockTunnel machine input identity differs.");
        void Source(OpeningItemSourceRef source) => Require(source is not null &&
            !string.IsNullOrWhiteSpace(source.Symbol) && source.StartLine > 0 && source.EndLine >= source.StartLine &&
            inputs.TryGetValue(source.Path, out var input) && input.Sha256 == source.Sha256,
            "RockTunnel machine source reference differs.");
        foreach (var source in document.RulesSources) Source(source);
        string[] symbols = ["TM39", "TM05", "TM09", "TM46", "TM03", "TM45", "TM43", "TM28", "TM31", "TM44", "HM01", "TM34", "HM05", "TM40"];
        for (int n = 0; n < document.Machines.Length; n++)
        {
            var machine = document.Machines[n];
            Require(machine.Machine.Item.Id.Value == "pret:ITEM_" + symbols[n] && machine.Compatibility.Length == 93,
                "RockTunnel machine order or compatibility differs.");
            Source(machine.Machine.Item.Source); Source(machine.Machine.DescriptionSource);
            foreach (var compatibility in machine.Compatibility) Source(compatibility.Source);
        }
        for (int n = 0; n < document.Icons.Length; n++)
        {
            var icon = document.Icons[n];
            Source(icon.Source); Source(icon.PaletteSource); Source(icon.BindingSource);
            Require(icon.Item.Value == "pret:ITEM_" + "TM40" &&
                icon.PngPath == "item_" + "TM40".ToLowerInvariant() + ".png",
                "RockTunnel machine icon identity differs.");
            _ = PretPewterMachines.IconRgba(icon);
        }
        return document;
    }

    public static PretRockTunnelMachinesDocument LoadProject(string root)
    {
        _ = Read(root, "scripts/rocktunnel-items.lock.json", LockSha256);
        _ = Read(root, "content/raw/rocktunnel-items/manifest.json", RawManifestSha256);
        using var manifest = JsonDocument.Parse(Read(root, "content/generated/rocktunnel-items/manifest.json", ManifestSha256));
        foreach (var output in manifest.RootElement.GetProperty("outputs").EnumerateArray())
            _ = Read(root, "content/generated/rocktunnel-items/" + output.GetProperty("path").GetString(),
                output.GetProperty("sha256").GetString()!, output.GetProperty("byteLength").GetInt32());
        var document = Load(Utf8.GetString(Read(root,
            "content/generated/rocktunnel-items/rocktunnel-machines.json", GeneratedSha256)));
        foreach (var input in document.Inputs)
            Input(root, "content/raw/rocktunnel-items/", input.Path, input.ByteLength, input.GitBlobSha1, input.Sha256);
        return document;
    }

    public static PewterMachineCatalog LoadCatalogProject(string root)
    {
        var document = LoadProject(root);
        var old = PretRoute11Machines.LoadCatalogProject(root);
        var legacy = new[] { PewterMachineRules.Definition(old, PewterMachineRules.Tm39Id)! }
            .Concat(old.AdditionalMachines!.Value).ToArray();
        for (int n = 0; n < legacy.Length; n++)
        {
            var prior = legacy[n]; var current = document.Machines[n];
            Require(prior.Machine == current.Machine && prior.Move == current.Move &&
                prior.SourceMoveId == current.SourceMoveId && prior.MovePp == current.MovePp &&
                prior.Compatibility.SequenceEqual(current.Compatibility.Take(79)),
                "RockTunnel changed historical machine metadata or compatibility.");
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
        Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {length}\0"), .. bytes])) == blob, "RockTunnel machine Git blob differs: " + path);
    }
    private static byte[] Read(string root, string path, string hash, int? length = null)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes && (length is null || info.Length == length), "RockTunnel machine file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        Require(Hash(bytes) == hash, "RockTunnel machine file differs: " + path);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe RockTunnel machine path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("RockTunnel machine directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("RockTunnel machine symlink rejected.");
        }
        return result;
    }
    private static string Hash(byte[] b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
