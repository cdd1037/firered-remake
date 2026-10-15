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
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretRockTunnelMachinesDocument Load(string json)
    {
        var document = ContentJson.ParseStrict<PretRockTunnelMachinesDocument>(json, ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 &&
            document.Machines.Length == 14 && document.Icons.Length == 1 && !document.RulesSources.IsDefaultOrEmpty && !document.Limitations.IsDefaultOrEmpty,
            "RockTunnel machine scope differs.");
        string[] symbols = ["TM39", "TM05", "TM09", "TM46", "TM03", "TM45", "TM43", "TM28", "TM31", "TM44", "HM01", "TM34", "HM05", "TM40"];
        for (int n = 0; n < document.Machines.Length; n++)
        {
            var machine = document.Machines[n];
            Require(machine.Machine.Item.Id.Value == "pret:ITEM_" + symbols[n] && machine.Compatibility.Length == 93,
                "RockTunnel machine order or compatibility differs.");
        }
        for (int n = 0; n < document.Icons.Length; n++)
        {
            var icon = document.Icons[n];
            Require(icon.Item.Value == "pret:ITEM_" + "TM40" &&
                icon.PngPath == "item_" + "TM40".ToLowerInvariant() + ".png",
                "RockTunnel machine icon identity differs.");
            PretPewterMachines.ValidateIcon(icon);
        }
        return document;
    }

    public static PretRockTunnelMachinesDocument LoadProject(string root)
    {
        var document = Load(Utf8.GetString(Read(root,            "content/generated/rocktunnel-items/rocktunnel-machines.json")));
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
            Profile = Profile,Compatibility = document.Machines[0].Compatibility,AdditionalMachines = document.Machines.Skip(1).ToImmutableArray()
        };
        PewterMachineRules.ValidateCatalog(result);
        return result;
    }
    private static byte[] Read(string root,string path)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes , "RockTunnel machine file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
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
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
