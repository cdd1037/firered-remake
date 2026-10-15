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
        var document=ContentJson.ParseStrict<PretRockTunnelMachinesDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        PretCeladonApproachSource.Require(document.SchemaVersion==1&&document.Machines.Length==15&&document.Icons.Length==1&&!document.Limitations.IsDefaultOrEmpty,
            "Celadon Gym machine scope differs.");
        PretCeladonApproachSource.Require(document.Inputs.SequenceEqual(items.Inputs)&&System.Text.Json.JsonSerializer.Serialize(document.Icons)==System.Text.Json.JsonSerializer.Serialize(items.Icons),"Celadon Gym machine/item provenance differs.");
        var old=PretCeladonApproachMachines.LoadProject(dependencyProjectRoot??root);
        for(int index=0;index<document.Machines.Length;index++)
        {
            var current=document.Machines[index];            PretCeladonApproachSource.Require(current.Compatibility.Length==98,"Celadon Gym compatibility count differs.");
            if(index>=14)continue;
            var prior=old.Machines[index];
            PretCeladonApproachSource.Require(prior.Machine==current.Machine&&prior.Move==current.Move&&prior.SourceMoveId==current.SourceMoveId&&
                prior.MovePp==current.MovePp&&prior.Compatibility.SequenceEqual(current.Compatibility.Take(94)),"Celadon Gym changed inherited machine data.");
        }
        return document;
    }
    public static PewterMachineCatalog LoadCatalogProject(string root,string? dependencyProjectRoot=null)
    {
        var document=LoadProject(root,dependencyProjectRoot);var prior=PretCeladonApproachMachines.LoadCatalogProject(dependencyProjectRoot??root);
        var result=prior with{Profile=Profile,Compatibility=document.Machines[0].Compatibility,AdditionalMachines=document.Machines.Skip(1).ToImmutableArray()};
        CeladonGymMachineRules.ValidateCatalog(result);return result;
    }
}
