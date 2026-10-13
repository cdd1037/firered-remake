using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Original Six Island source proof retained for content authentication.</summary>
public static class SixIslandItemEvolutionRules
{
    public const string SunStone = "ITEM_SUN_STONE";
    public static SpeciesId Gloom { get; } = new("pret:SPECIES_GLOOM");
    public static SpeciesId Bellossom { get; } = new("pret:SPECIES_BELLOSSOM");

    public static PokemonSourceRef SourceEdge(SixIslandOwnedPokemonCatalog owned)
    {
        if (!ReferenceEquals(SixIslandOwnedPokemonCatalog.For(owned.Catalog), owned)) throw new ArgumentException("six_island_item_evolution_catalog");
        var matches = owned.Mechanics.SpeciesMechanics[Gloom].SourceEvolutions.Where(e => e.Method == "EVO_ITEM" &&
            e.SourceMethodId == 7 && e.Parameter == SunStone && e.ParameterValue == 93 && e.Target == Bellossom).ToArray();
        if (matches.Length != 1 || !owned.Catalog.Species.ContainsKey(Bellossom)) throw new ArgumentException("six_island_item_evolution_source");
        return matches[0].Source;
    }
}
