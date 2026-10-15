using System.Collections.Immutable;
using System.Text;
using Game.Domain.Items;
namespace Game.Content;

public static class PretCeladonGymMachines
{
    public const string Profile=CeladonGymMachineRules.Profile;
    public static PretRockTunnelMachinesDocument LoadProject(string root,string? dependencyProjectRoot=null)
    {
        var items=PretCeladonGymItems.LoadProject(root);
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/celadon-gym-items/celadon-gym-machines.json");
        var document=ContentJson.ParseStrict<PretRockTunnelMachinesDocument>(Encoding.UTF8.GetString(bytes),SharedMachineCatalog.Options(root));
        PretCeladonApproachSource.Require(document.SchemaVersion==1&&document.Machines.Length==15&&document.Icons.Length==1&&!document.Limitations.IsDefaultOrEmpty,
            "Celadon Gym machine scope differs.");
        PretCeladonApproachSource.Require(document.Inputs.SequenceEqual(items.Inputs)&&System.Text.Json.JsonSerializer.Serialize(document.Icons)==System.Text.Json.JsonSerializer.Serialize(items.Icons),"Celadon Gym machine/item provenance differs.");
        PretCeladonApproachSource.Require(document.Machines.All(m => m.Compatibility.Length == 98), "Machine membership differs.");
        return document;
    }
    public static PewterMachineCatalog LoadCatalogProject(string root,string? dependencyProjectRoot=null)
    {
        var document=LoadProject(root,dependencyProjectRoot);var prior=PretPewterMachines.LoadProject(dependencyProjectRoot??root);
        var result=prior with{Profile=Profile,Compatibility=document.Machines[0].Compatibility,AdditionalMachines=document.Machines.Skip(1).ToImmutableArray()};
        CeladonGymMachineRules.ValidateCatalog(result);return result;
    }
}
