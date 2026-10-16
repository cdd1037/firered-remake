using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretCeruleanMachinesDocument(int SchemaVersion,
    ImmutableArray<MachineDefinition> Machines, PretPewterMachineIcon Icon);

/// <summary>Loads five explicit TMs for 65 species.</summary>
public static class PretCeruleanMachines
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretCeruleanMachinesDocument Load(string json, SharedMachineCatalog machines)
    {
        var document = ContentJson.ParseStrict<PretCeruleanMachinesDocument>(json, machines.CreateJsonOptions());
        Require(document.SchemaVersion == 1 &&
            document.Machines.Length == 5,
            "Cerulean machine scope differs.");
        string[] symbols = ["TM39", "TM05", "TM09", "TM46", "TM03"];
        for (int n = 0; n < document.Machines.Length; n++)
        {
            var machine = document.Machines[n];
            Require(machine.Machine.Item.Id.Value == "pret:ITEM_" + symbols[n] && machine.Compatibility.Length == 65,
                "Cerulean machine order or compatibility differs.");
        }
        Require(document.Icon.Item == MachineRules.Tm03Id && document.Icon.Region is not null,
            "Cerulean machine icon identity differs.");
        PretPewterMachines.ValidateIcon(document.Icon);
        return document;
    }

    public static PretCeruleanMachinesDocument LoadProject(string root)
    {
        var document = Load(Utf8.GetString(Read(root,            "content/generated/data/machine-learning/cerulean.json")), SharedMachineCatalog.LoadProject(root));
        return document;
    }

    private static byte[] Read(string root,string path)
    {
        var p = GeneratedResourceFile.SafePath(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes , "Cerulean machine file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        return bytes;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
