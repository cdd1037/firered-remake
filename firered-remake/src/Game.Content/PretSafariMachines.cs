using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

/// <summary>Exact24-machine,110-species source candidate. The caller applies Domain authentication before teaching.</summary>
public static class PretSafariMachines
{
    public const string Profile="firered-safari-teaching-v1";
    public static PewterMachineCatalog Extend(PewterMachineCatalog previous,PretSafariPokemon pokemon,PretSafariItems items)
    {
        FuchsiaGymMachineRules.ValidateCatalog(previous);
        ArgumentNullException.ThrowIfNull(pokemon);ArgumentNullException.ThrowIfNull(items);
        SafariSource.Require(pokemon.Catalog.Species.Count==110&&pokemon.Document.Machines.Length==3&&
            pokemon.Document.Machines.Select(r=>pokemon.Catalog.Species[r.Species].SourceSpeciesId).SequenceEqual([113,123,128]),"Safari machine species scope differs.");
        var source=items.Machines;
        var oldIds=new[]{previous.Tm39.Item.Id}.Concat(previous.AdditionalMachines!.Value.Select(m=>m.Machine.Item.Id));
        SafariSource.Require(oldIds.SequenceEqual(source.InheritedItemIds),"Safari inherited machine order differs.");
        ImmutableArray<PewterMachineCompatibility> Rows(ItemId item,ImmutableArray<PewterMachineCompatibility> inherited)
        {
            var added=source.Additions.Single(a=>a.Item==item).Compatibility;
            foreach(var cell in added)
            {
                var row=pokemon.Document.Machines.Single(m=>m.Species==cell.Species);
                SafariSource.Require(cell.CanLearn==row.MachineSymbols.Any(s=>s.StartsWith(item.Value[10..]+"_",StringComparison.Ordinal))&&
                    cell.Source==new OpeningItemSourceRef(row.Source.Path,row.Source.Symbol,row.Source.StartLine,row.Source.EndLine,row.Source.Sha256),"Safari species/item source compatibility differs.");
            }
            return inherited.AddRange(added);
        }
        var targetOrder=previous.Compatibility.Select(c=>c.Species).Concat(pokemon.Document.Machines.Select(m=>m.Species)).ToArray();
        var additions=source.NewMachines.Select(m=>
        {
            SafariSource.Require(m.Compatibility.Select(c=>c.Species).ToHashSet().SetEquals(pokemon.Catalog.Species.Keys),"Safari HM exact110-species set differs.");
            var rows=m.Compatibility.ToDictionary(c=>c.Species);
            return m with{Compatibility=targetOrder.Select(id=>rows[id]).ToImmutableArray()};
        });
        return previous with{Profile=Profile,Compatibility=Rows(previous.Tm39.Item.Id,previous.Compatibility),
            AdditionalMachines=previous.AdditionalMachines.Value.Select(m=>m with{Compatibility=Rows(m.Machine.Item.Id,m.Compatibility)}).Concat(additions).ToImmutableArray()};
    }
}
