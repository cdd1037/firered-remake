using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;
public static class PretPokemonTowerMachines
{
    public static PewterMachineCatalog Extend(string root,PewterMachineCatalog previous)
    {
        RocketHideoutMachineRules.ValidateCatalog(previous);
        var bytes=PretCeladonApproachSource.Read(root,PretSourcePaths.FireRedRawDirectory + "/"+PokemonTowerMachineRules.SourcePath);
        string raw=Encoding.UTF8.GetString(bytes);
        ImmutableArray<PewterMachineCompatibility> Rows(ItemId item,ImmutableArray<PewterMachineCompatibility> old)
        {
            foreach(var (name,id) in new[]{("GASTLY",92),("HAUNTER",93)})
            {
                string symbol="SPECIES_"+name;var m=Regex.Match(raw,@"\["+symbol+@"\]\s*=\s*TMHM_LEARNSET\((.*?)\),",RegexOptions.Singleline|RegexOptions.CultureInvariant);
                if(!m.Success)throw new InvalidDataException("tower_machine_species_source");
                bool compatible=Regex.Matches(m.Value,@"TMHM\((TM\d\d|HM\d\d)_",RegexOptions.CultureInvariant).Any(x=>"pret:ITEM_"+x.Groups[1].Value==item.Value);
                int line=raw.AsSpan(0,m.Index).Count('\n')+1,last=line+m.Value.Count(c=>c=='\n');
                old=old.Add(new(new("pret:"+symbol),id,compatible,new(PokemonTowerMachineRules.SourcePath,symbol,line,last)));
            }
            return old;
        }
        var result=previous with{Profile=PokemonTowerMachineRules.Profile,Compatibility=Rows(previous.Tm39.Item.Id,previous.Compatibility),AdditionalMachines=previous.AdditionalMachines!.Value.Select(m=>m with{Compatibility=Rows(m.Machine.Item.Id,m.Compatibility)}).ToImmutableArray()};
        PokemonTowerMachineRules.ValidateCatalog(result);return result;
    }
}
