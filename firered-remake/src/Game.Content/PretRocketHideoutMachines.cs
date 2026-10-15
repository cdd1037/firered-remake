using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;
namespace Game.Content;

public static class PretRocketHideoutMachines
{
    public static PretRockTunnelMachinesDocument LoadProject(string root, string? dependencyProjectRoot = null)
    {
        var items = PretRocketHideoutItems.LoadProject(root);
        var bytes = PretCeladonApproachSource.Read(root,"content/generated/data/items/rocket-hideout/rocket-hideout-machines.json");
        var document = ContentJson.ParseStrict<PretRockTunnelMachinesDocument>(Encoding.UTF8.GetString(bytes),SharedMachineCatalog.Options(root));
        PretCeladonApproachSource.Require(document.SchemaVersion == 1 && document.Machines.Length == 18 && document.Icons.Length == 10,
            "Rocket Hideout machine scope differs.");
        PretCeladonApproachSource.Require(document.Machines.All(m => m.Compatibility.Length == 99), "Machine membership differs.");
        return document;
    }
}
