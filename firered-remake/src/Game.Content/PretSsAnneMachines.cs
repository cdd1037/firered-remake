using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretSsAnneMachinesDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PewterAdditionalMachine> Machines, ImmutableArray<PretPewterMachineIcon> Icons, ImmutableArray<string> Limitations);

/// <summary>Authenticates eleven explicit TM/HMs for 73 species, preserving every historical machine profile.</summary>
public static class PretSsAnneMachines
{
    public const string Profile = PewterMachineRules.SsAnneProfile;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretSsAnneMachinesDocument Load(string json)
    {
        var document = ContentJson.ParseStrict<PretSsAnneMachinesDocument>(json, ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 &&
            document.Machines.Length == 11 && document.Icons.Length == 3 && !document.RulesSources.IsDefaultOrEmpty && !document.Limitations.IsDefaultOrEmpty,
            "SsAnne machine scope differs.");
        string[] symbols = ["TM39", "TM05", "TM09", "TM46", "TM03", "TM45", "TM43", "TM28", "TM31", "TM44", "HM01"];
        for (int n = 0; n < document.Machines.Length; n++)
        {
            var machine = document.Machines[n];
            Require(machine.Machine.Item.Id.Value == "pret:ITEM_" + symbols[n] && machine.Compatibility.Length == 73,
                "SsAnne machine order or compatibility differs.");
        }
        for (int n = 0; n < document.Icons.Length; n++)
        {
            var icon = document.Icons[n];
            Require(icon.Item.Value == "pret:ITEM_" + symbols[n + 8] &&
                icon.PngPath == "item_" + symbols[n + 8].ToLowerInvariant() + ".png",
                "SsAnne machine icon identity differs.");
            PretPewterMachines.ValidateIcon(icon);
        }
        return document;
    }

    public static PretSsAnneMachinesDocument LoadProject(string root)
    {
        var document = Load(Utf8.GetString(Read(root,            "content/generated/ssanne-machines/ssanne-machines.json")));
        return document;
    }

    public static PewterMachineCatalog LoadCatalogProject(string root)
    {
        var document = LoadProject(root);
        var old = PretVermilionMachines.LoadCatalogProject(root);
        var legacy = new[] { PewterMachineRules.Definition(old, PewterMachineRules.Tm39Id)! }
            .Concat(old.AdditionalMachines!.Value).ToArray();
        for (int n = 0; n < legacy.Length; n++)
        {
            var prior = legacy[n]; var current = document.Machines[n];
            Require(prior.Machine == current.Machine && prior.Move == current.Move &&
                prior.SourceMoveId == current.SourceMoveId && prior.MovePp == current.MovePp &&
                prior.Compatibility.SequenceEqual(current.Compatibility.Take(70)),
                "SsAnne changed historical machine metadata or compatibility.");
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
        Require(info.Length <= ContentJson.MaximumJsonBytes , "SsAnne machine file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe SsAnne machine path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("SsAnne machine directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("SsAnne machine symlink rejected.");
        }
        return result;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
