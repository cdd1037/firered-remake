using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretCeruleanMachinesDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<PewterAdditionalMachine> Machines, PretPewterMachineIcon Icon, ImmutableArray<string> Limitations);

/// <summary>Authenticates five explicit TMs for 65 species, preserving every historical machine profile.</summary>
public static class PretCeruleanMachines
{
    public const string Profile = PewterMachineRules.CeruleanProfile;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretCeruleanMachinesDocument Load(string json, SharedMachineCatalog machines)
    {
        var document = ContentJson.ParseStrict<PretCeruleanMachinesDocument>(json, machines.CreateJsonOptions());
        Require(document.SchemaVersion == 1 &&
            document.Machines.Length == 5 && !document.Limitations.IsDefaultOrEmpty,
            "Cerulean machine scope differs.");
        string[] symbols = ["TM39", "TM05", "TM09", "TM46", "TM03"];
        for (int n = 0; n < document.Machines.Length; n++)
        {
            var machine = document.Machines[n];
            Require(machine.Machine.Item.Id.Value == "pret:ITEM_" + symbols[n] && machine.Compatibility.Length == 65,
                "Cerulean machine order or compatibility differs.");
        }
        Require(document.Icon.Item == PewterMachineRules.Tm03Id && document.Icon.Region is not null,
            "Cerulean machine icon identity differs.");
        PretPewterMachines.ValidateIcon(document.Icon);
        return document;
    }

    public static PretCeruleanMachinesDocument LoadProject(string root)
    {
        var document = Load(Utf8.GetString(Read(root,            "content/generated/cerulean-machines/cerulean-machines.json")), SharedMachineCatalog.LoadProject(root));
        return document;
    }

    public static PewterMachineCatalog LoadCatalogProject(string root)
    {
        var document = LoadProject(root);
        var old = PretPewterMachines.LoadProject(root);
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
        Require(info.Length <= ContentJson.MaximumJsonBytes , "Cerulean machine file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Cerulean machine path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("Cerulean machine directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Cerulean machine symlink rejected.");
        }
        return result;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
