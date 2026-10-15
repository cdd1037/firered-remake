using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;
public static class PretPokemonTowerMachines
{
    public static PewterMachineCatalog Extend(string root,PewterMachineCatalog previous)
    {
        RocketHideoutMachineRules.ValidateCatalog(previous);
        var additions = ContentJson.ParseStrict<ImmutableArray<VermilionMachineRow>>(
            File.ReadAllText(Path.Combine(root, "content/generated/pokemon-tower-machines/compatibility.json")), SharedMachineCatalog.Options(root))
            .ToImmutableDictionary(row => row.Item, row => row.Compatibility);
        ImmutableArray<PewterMachineCompatibility> Rows(ItemId item, ImmutableArray<PewterMachineCompatibility> rows) =>
            rows.AddRange(additions.TryGetValue(item, out var extra) ? extra : throw new InvalidDataException("Machine compatibility missing: " + item.Value));
        var result=previous with{Profile=PokemonTowerMachineRules.Profile,Compatibility=Rows(previous.Tm39.Item.Id,previous.Compatibility),AdditionalMachines=previous.AdditionalMachines!.Value.Select(m=>m with{Compatibility=Rows(m.Machine.Item.Id,m.Compatibility)}).ToImmutableArray()};
        PokemonTowerMachineRules.ValidateCatalog(result);return result;
    }
}
