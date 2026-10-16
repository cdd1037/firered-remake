using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretRoute11MachinesDocument(int SchemaVersion,
    ImmutableArray<MachineDefinition> Machines, ImmutableArray<PretPewterMachineIcon> Icons);

/// <summary>Loads thirteen explicit TM/HMs for 79 species.</summary>
public static class PretRoute11Machines
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretRoute11MachinesDocument Load(string json, SharedMachineCatalog machines)
    {
        var document = ContentJson.ParseStrict<PretRoute11MachinesDocument>(json, machines.CreateJsonOptions());
        Require(document.SchemaVersion == 1 &&
            document.Machines.Length == 13 && document.Icons.Length == 1,
            "Route11 machine scope differs.");
        string[] symbols = ["TM39", "TM05", "TM09", "TM46", "TM03", "TM45", "TM43", "TM28", "TM31", "TM44", "HM01", "TM34", "HM05"];
        for (int n = 0; n < document.Machines.Length; n++)
        {
            var machine = document.Machines[n];
            Require(machine.Machine.Item.Id.Value == "pret:ITEM_" + symbols[n] && machine.Compatibility.Length == 79,
                "Route11 machine order or compatibility differs.");
        }
        for (int n = 0; n < document.Icons.Length; n++)
        {
            var icon = document.Icons[n];
            Require(icon.Item.Value == "pret:ITEM_" + "HM05" &&
                icon.Region is not null,
                "Route11 machine icon identity differs.");
            PretPewterMachines.ValidateIcon(icon);
        }
        return document;
    }

    public static PretRoute11MachinesDocument LoadProject(string root)
    {
        var document = Load(Utf8.GetString(Read(root,            "content/generated/data/machine-learning/route11.json")), SharedMachineCatalog.LoadProject(root));
        return document;
    }

    private static byte[] Read(string root,string path)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes , "Route11 machine file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
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
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
