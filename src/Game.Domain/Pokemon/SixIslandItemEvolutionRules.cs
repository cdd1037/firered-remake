using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Six Island item evolution rule, including its supported target and item condition.</summary>
public static class SixIslandItemEvolutionRules
{
    public const string SunStone = "ITEM_SUN_STONE";
    public static SpeciesId Gloom { get; } = new("pret:SPECIES_GLOOM");
    public static SpeciesId Bellossom { get; } = new("pret:SPECIES_BELLOSSOM");

    public static SourceMechanicsEvolution EvolutionEdge(GamePokemonCatalog owned)
    {
        var matches = owned.Mechanics.SpeciesMechanics[Gloom].SourceEvolutions.Where(e => e.Method == "EVO_ITEM" &&
            e.SourceMethodId == 7 && e.Parameter == SunStone && e.ParameterValue == 93 && e.Target == Bellossom).ToArray();
        if (matches.Length != 1 || !owned.Catalog.Species.ContainsKey(Bellossom)) throw new ArgumentException("six_island_item_evolution_source");
        return matches[0];
    }
}
