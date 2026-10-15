// Reconstructed recovery source. Generated hash is bound to the reproduced historical manifest.
using System.Collections.Immutable;
using System.Text;
using Game.Domain.Items;
namespace Game.Content;
public static class PretCeladonApproachMachines
{
    public const string Profile="firered-celadon-approach-machines-v1";
    public static PretRockTunnelMachinesDocument LoadProject(string root)
    {
        _=PretCeladonApproachItems.LoadProject(root);
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/celadon-approach-items/celadon-approach-machines.json");
        var d=ContentJson.ParseStrict<PretRockTunnelMachinesDocument>(Encoding.UTF8.GetString(bytes),SharedMachineCatalog.Options(root));
        PretCeladonApproachSource.Require(d.SchemaVersion==1&&d.Machines.Length==14&&d.Icons.IsEmpty&&!d.Limitations.IsDefaultOrEmpty,"Celadon machine scope differs.");
        PretCeladonApproachSource.Require(d.Machines.All(m => m.Compatibility.Length == 94), "Celadon machine membership differs.");
        return d;
    }
    public static PewterMachineCatalog LoadCatalogProject(string root)
    {
        var d=LoadProject(root);var old=PretPewterMachines.LoadProject(root);
        var result=old with{Profile=Profile,Compatibility=d.Machines[0].Compatibility,AdditionalMachines=d.Machines.Skip(1).ToImmutableArray()};
        PewterMachineRules.ValidateCatalog(result);return result;
    }
}
