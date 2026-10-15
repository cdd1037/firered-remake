using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretBillMachinesDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<PewterAdditionalMachine> Machines, ImmutableArray<PretPewterMachineIcon> Icons, ImmutableArray<string> Limitations);

/// <summary>Authenticates eight explicit TMs for 68 species, preserving every historical machine profile.</summary>
public static class PretBillMachines
{
    public const string Profile = PewterMachineRules.BillProfile;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretBillMachinesDocument Load(string json, SharedMachineCatalog machines)
    {
        var document = ContentJson.ParseStrict<PretBillMachinesDocument>(json, machines.CreateJsonOptions());
        Require(document.SchemaVersion == 1 &&
            document.Machines.Length == 8 && document.Icons.Length == 3 && !document.Limitations.IsDefaultOrEmpty,
            "Bill machine scope differs.");
        string[] symbols = ["TM39", "TM05", "TM09", "TM46", "TM03", "TM45", "TM43", "TM28"];
        for (int n = 0; n < document.Machines.Length; n++)
        {
            var machine = document.Machines[n];
            Require(machine.Machine.Item.Id.Value == "pret:ITEM_" + symbols[n] && machine.Compatibility.Length == 68,
                "Bill machine order or compatibility differs.");
        }
        for (int n = 0; n < document.Icons.Length; n++)
        {
            var icon = document.Icons[n];
            Require(icon.Item.Value == "pret:ITEM_" + symbols[n + 5] &&
                icon.Region is not null,
                "Bill machine icon identity differs.");
            PretPewterMachines.ValidateIcon(icon);
        }
        return document;
    }

    public static PretBillMachinesDocument LoadProject(string root)
    {
        var document = Load(Utf8.GetString(Read(root,            "content/generated/bill-machines/bill-machines.json")), SharedMachineCatalog.LoadProject(root));
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
        Require(info.Length <= ContentJson.MaximumJsonBytes , "Bill machine file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
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
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
