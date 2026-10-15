using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;

public static class PretEeveeMachines
{
    public static PewterMachineCatalog Extend(string projectRoot,PewterMachineCatalog previous)
    {
        SnorlaxMachineRules.ValidateCatalog(previous);
        var bytes=PretCeladonApproachSource.Read(projectRoot,PretSourcePaths.FireRedRawDirectory + "/"+EeveeMachineRules.SourcePath);
        string raw=Encoding.UTF8.GetString(bytes);
        var match=Regex.Match(raw,@"\[SPECIES_EEVEE\]\s*=\s*TMHM_LEARNSET\((.*?)\),",RegexOptions.Singleline|RegexOptions.CultureInvariant);
        if (!match.Success) throw new InvalidDataException("eevee_machine_species_source");
        var compatible=Regex.Matches(match.Value,@"TMHM\((TM\d\d|HM\d\d)_",RegexOptions.CultureInvariant)
            .Select(m=>"pret:ITEM_"+m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        int line=raw.AsSpan(0,match.Index).Count('\n')+1,last=line+match.Value.Count(c=>c=='\n');
        ImmutableArray<PewterMachineCompatibility> Rows(ItemId item,ImmutableArray<PewterMachineCompatibility> rows) => rows.Add(
            new(new("pret:SPECIES_EEVEE"),133,compatible.Contains(item.Value),new(EeveeMachineRules.SourcePath,"SPECIES_EEVEE",line,last)));
        var result=previous with { Profile=EeveeMachineRules.Profile,Compatibility=Rows(previous.Tm39.Item.Id,previous.Compatibility),AdditionalMachines=previous.AdditionalMachines!.Value.Select(m=>m with {Compatibility=Rows(m.Machine.Item.Id,m.Compatibility)}).ToImmutableArray() };
        EeveeMachineRules.ValidateCatalog(result); return result;
    }
}
