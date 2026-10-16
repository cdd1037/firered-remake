using System.Collections.Immutable;
using System.Text;
using Game.Domain.Items;
namespace Game.Content;

public static class PretCeladonGymMachines
{
    public static PretRockTunnelMachinesDocument LoadProject(string root,string? dependencyProjectRoot=null)
    {
        var items=PretCeladonGymItems.LoadProject(root);
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/data/items/celadon-gym/celadon-gym-machines.json");
        var document=ContentJson.ParseStrict<PretRockTunnelMachinesDocument>(Encoding.UTF8.GetString(bytes),SharedMachineCatalog.Options(root));
        PretCeladonApproachSource.Require(document.SchemaVersion==1&&document.Machines.Length==15&&document.Icons.Length==1,
            "Celadon Gym machine scope differs.");
        PretCeladonApproachSource.Require(System.Text.Json.JsonSerializer.Serialize(document.Icons)==System.Text.Json.JsonSerializer.Serialize(items.Icons),"Celadon Gym machine/item provenance differs.");
        PretCeladonApproachSource.Require(document.Machines.All(m => m.Compatibility.Length == 98), "Machine membership differs.");
        return document;
    }
}
