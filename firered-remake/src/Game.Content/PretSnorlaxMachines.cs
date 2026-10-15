using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;

public static class PretSnorlaxMachines
{
    public static PewterMachineCatalog Extend(string projectRoot,PewterMachineCatalog previous)
    {
        PokemonTowerMachineRules.ValidateCatalog(previous);
        var additions = ContentJson.ParseStrict<ImmutableArray<VermilionMachineRow>>(
            File.ReadAllText(Path.Combine(projectRoot, "content/generated/snorlax-machines/compatibility.json")), SharedMachineCatalog.Options(projectRoot))
            .ToImmutableDictionary(row => row.Item, row => row.Compatibility);
        ImmutableArray<PewterMachineCompatibility> Rows(ItemId item, ImmutableArray<PewterMachineCompatibility> rows) =>
            rows.AddRange(additions.TryGetValue(item, out var extra) ? extra : throw new InvalidDataException("Machine compatibility missing: " + item.Value));
        var result=previous with { Profile=SnorlaxMachineRules.Profile,Compatibility=Rows(previous.Tm39.Item.Id,previous.Compatibility),AdditionalMachines=previous.AdditionalMachines!.Value.Select(m=>m with {Compatibility=Rows(m.Machine.Item.Id,m.Compatibility)}).ToImmutableArray() };
        SnorlaxMachineRules.ValidateCatalog(result); return result;
    }
}
