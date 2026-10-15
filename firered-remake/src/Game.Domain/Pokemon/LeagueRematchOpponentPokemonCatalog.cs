using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class LeagueRematchOpponentPokemonCatalog
{
    public const string Profile = "firered-league-rematch-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_CROBAT"), new("pret:SPECIES_MISDREAVUS"), new("pret:SPECIES_HERACROSS"),
         new("pret:SPECIES_KINGDRA"), new("pret:SPECIES_TYRANITAR")];
}
