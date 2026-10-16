using System.Collections.Immutable;

namespace Game.Content;

/// <summary>GBA inverse affine coefficients and signed sprite offsets, directly from the species entry.</summary>
public sealed record PokedexSizeTransform(int PokemonScale, int PokemonOffset, int TrainerScale, int TrainerOffset);
public sealed record PokedexHabitatSlot(int PortraitX,int PortraitY,int InfoX,int InfoY);
public sealed record PokedexHabitatDefinition(string Id, string Title, ImmutableArray<ImmutableArray<int>> Pages);
public sealed record PokedexHabitatPage(int SourcePage, ImmutableArray<PokedexMenuRow> Rows);
public sealed record PokedexHabitat(string Id, string Title, ImmutableArray<PokedexHabitatPage> Pages);

public sealed partial class PretPokedexMenu
{
    private void ValidateHabitats()
    {
        Require(!Document.Habitats.IsDefaultOrEmpty && Document.Habitats.Select(x=>x.Id).Distinct().Count()==Document.Habitats.Length,
            "Pokédex habitat definitions are invalid.");
        Require(!Document.HabitatLayouts.IsDefault && Document.HabitatLayouts.Length==4 &&
            Document.HabitatLayouts.Select((slots,index)=>!slots.IsDefault && slots.Length==index+1).All(x=>x),
            "Pokédex habitat layouts are invalid.");
        Require(!Document.SilhouetteRgb.IsDefault && Document.SilhouetteRgb.Length==3 && Document.SilhouetteRgb.All(x=>x is >=0 and <=255),"Invalid silhouette palette.");
        var all=new List<int>();
        foreach(var habitat in Document.Habitats)
        {
            Require(!string.IsNullOrWhiteSpace(habitat.Id)&&!string.IsNullOrWhiteSpace(habitat.Title)&&!habitat.Pages.IsDefaultOrEmpty,
                "Pokédex habitat is empty.");
            foreach(var page in habitat.Pages)
            {
                Require(!page.IsDefaultOrEmpty&&page.Length<=4&&page.All(Entries.ContainsKey),"Pokédex habitat page is invalid.");
                all.AddRange(page);
            }
        }
        Require(all.Count==Entries.Count&&all.Distinct().Count()==Entries.Count,"Pokédex habitats must partition the species catalog.");
        Require(Document.Sizes.Count==Entries.Count&&Document.Sizes.Keys.All(Entries.ContainsKey),"Incomplete size transforms.");
        foreach(var size in Document.Sizes.Values)
            Require(size is {PokemonScale:>0,TrainerScale:>0}&&size.PokemonOffset is >=-128 and <=127&&
                size.TrainerOffset is >=-128 and <=127,"Pokédex size transform is invalid.");
    }

    /// <summary>Source categories retain their order; unseen species and wholly unseen pages are omitted.</summary>
    public ImmutableArray<PokedexHabitat> BuildHabitats(IEnumerable<int> seenSpecies,IEnumerable<int> caughtSpecies,PokedexMenuAccess access)
    {
        Require(access.HasPokedex,"Pokédex has not been received.");
        var seen=ValidateSpecies(seenSpecies);var caught=ValidateSpecies(caughtSpecies);
        Require(caught.IsSubsetOf(seen),"Caught Pokédex species must also be seen.");
        int capacity=access.NationalEnabled?Document.Rules.NationalCount:Document.Rules.RegionalCount;
        return Document.Habitats.Select(h=>new PokedexHabitat(h.Id,h.Title,h.Pages.Select((p,index)=>
            new PokedexHabitatPage(index,p.Where(id=>seen.Contains(id)&&Entries[id].NationalNumber<=capacity)
                .Select(id=>BuildRow(Entries[id],seen,caught)).ToImmutableArray()))
            .Where(p=>p.Rows.Length!=0).ToImmutableArray())).ToImmutableArray();
    }

    /// <summary>Both silhouettes are absent for seen-but-not-caught species, including the trainer.</summary>
    public PokedexSizeTransform? BuildSizeComparison(int species,IEnumerable<int> seen,IEnumerable<int> caught,PokedexMenuAccess access)
        => BuildPage(species,seen,caught,access).Caught?Document.Sizes[species]:null;
}
