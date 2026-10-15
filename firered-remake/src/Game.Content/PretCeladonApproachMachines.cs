using System.Collections.Immutable;
using System.Text;
using Game.Domain.Items;
namespace Game.Content;
public static class PretCeladonApproachMachines
{
    public static PretRockTunnelMachinesDocument LoadProject(string root)
    {
        _=PretCeladonApproachItems.LoadProject(root);
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/data/items/celadon-approach/celadon-approach-machines.json");
        var d=ContentJson.ParseStrict<PretRockTunnelMachinesDocument>(Encoding.UTF8.GetString(bytes),SharedMachineCatalog.Options(root));
        PretCeladonApproachSource.Require(d.SchemaVersion==1&&d.Machines.Length==14&&d.Icons.IsEmpty,"Celadon machine scope differs.");
        PretCeladonApproachSource.Require(d.Machines.All(m => m.Compatibility.Length == 94), "Celadon machine membership differs.");
        return d;
    }
}
