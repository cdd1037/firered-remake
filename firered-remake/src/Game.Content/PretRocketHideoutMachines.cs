using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;
namespace Game.Content;

public static class PretRocketHideoutMachines
{
    public const string Profile = RocketHideoutMachineRules.Profile;
    public static PretRockTunnelMachinesDocument LoadProject(string root, string? dependencyProjectRoot = null)
    {
        var items = PretRocketHideoutItems.LoadProject(root);
        var bytes = PretCeladonApproachSource.Read(root,"content/generated/rocket-hideout-items/rocket-hideout-machines.json");
        var document = ContentJson.ParseStrict<PretRockTunnelMachinesDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        PretCeladonApproachSource.Require(document.SchemaVersion == 1 && document.Machines.Length == 18 && document.Icons.Length == 10 && !document.RulesSources.IsDefaultOrEmpty && !document.Limitations.IsDefaultOrEmpty,
            "Rocket Hideout machine scope differs.");
        PretCeladonApproachSource.Require(document.Inputs.SequenceEqual(items.Inputs) ,"Rocket Hideout machine/item provenance differs.");
        var old = PretCeladonGymMachines.LoadProject(dependencyProjectRoot ?? root);
        for (int index = 0; index < document.Machines.Length; index++)
        {
            var current = document.Machines[index];             PretCeladonApproachSource.Require(current.Compatibility.Length == 99,"Rocket Hideout compatibility count differs.");
            if (index >= 15) continue;
            var prior = old.Machines[index];
            PretCeladonApproachSource.Require(prior.Machine == current.Machine && prior.Move == current.Move && prior.SourceMoveId == current.SourceMoveId &&
                prior.MovePp == current.MovePp && prior.Compatibility.SequenceEqual(current.Compatibility.Take(98)),"Rocket Hideout changed inherited machine data.");
        }
        return document;
    }
    public static PewterMachineCatalog LoadCatalogProject(string root, string? dependencyProjectRoot = null)
    {
        var document = LoadProject(root,dependencyProjectRoot); var prior = PretCeladonGymMachines.LoadCatalogProject(dependencyProjectRoot ?? root);
        var result = prior with { Profile = Profile,Compatibility = document.Machines[0].Compatibility,AdditionalMachines = document.Machines.Skip(1).ToImmutableArray() };
        RocketHideoutMachineRules.ValidateCatalog(result); return result;
    }
}
