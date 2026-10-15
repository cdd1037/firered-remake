using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretSsAnneMachinesDocument(int SchemaVersion,
    ImmutableArray<MachineDefinition> Machines, ImmutableArray<PretPewterMachineIcon> Icons);

/// <summary>Loads eleven explicit TM/HMs for 73 species.</summary>
public static class PretSsAnneMachines
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretSsAnneMachinesDocument Load(string json, SharedMachineCatalog machines)
    {
        var document = ContentJson.ParseStrict<PretSsAnneMachinesDocument>(json, machines.CreateJsonOptions());
        Require(document.SchemaVersion == 1 &&
            document.Machines.Length == 11 && document.Icons.Length == 3,
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
                icon.Region is not null,
                "SsAnne machine icon identity differs.");
            PretPewterMachines.ValidateIcon(icon);
        }
        return document;
    }

    public static PretSsAnneMachinesDocument LoadProject(string root)
    {
        var document = Load(Utf8.GetString(Read(root,            "content/generated/data/machine-learning/ssanne.json")), SharedMachineCatalog.LoadProject(root));
        return document;
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
