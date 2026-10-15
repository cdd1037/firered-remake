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
        var d=ContentJson.ParseStrict<PretRockTunnelMachinesDocument>(Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        PretCeladonApproachSource.Require(d.SchemaVersion==1&&d.Machines.Length==14&&d.Icons.IsEmpty&&!d.Limitations.IsDefaultOrEmpty,"Celadon machine scope differs.");
        var old=PretRockTunnelMachines.LoadProject(root);
        for(int i=0;i<14;i++)
        {
            var a=old.Machines[i];var b=d.Machines[i];
            PretCeladonApproachSource.Require(a.Machine==b.Machine&&a.Move==b.Move&&a.SourceMoveId==b.SourceMoveId&&a.MovePp==b.MovePp&&b.Compatibility.Length==94&&a.Compatibility.SequenceEqual(b.Compatibility.Take(93)),"Inherited machine changed.");
            var tail=b.Compatibility[^1];
            PretCeladonApproachSource.Require(tail.Species.Value=="pret:SPECIES_MUK"&&tail.SourceSpeciesId==89&&tail.CanLearn==("10010111110100"[i]=='1')&&
                tail.Source==new OpeningItemSourceRef("src/data/pokemon/tmhm_learnsets.h","SPECIES_MUK",2035,2063),"Muk source compatibility differs.");
        }
        return d;
    }
    public static PewterMachineCatalog LoadCatalogProject(string root)
    {
        var d=LoadProject(root);var old=PretRockTunnelMachines.LoadCatalogProject(root);
        var result=old with{Profile=Profile,Compatibility=d.Machines[0].Compatibility,AdditionalMachines=d.Machines.Skip(1).ToImmutableArray()};
        PewterMachineRules.ValidateCatalog(result);return result;
    }
}
