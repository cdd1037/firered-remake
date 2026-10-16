using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class OrdinaryEvolutionOwnedPokemonCatalog
{
    public const string Profile = "firered-ordinary-evolution-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_RHYDON"), new("pret:SPECIES_SLOWBRO"), new("pret:SPECIES_CROBAT"), new("pret:SPECIES_BLISSEY")];
    public static ImmutableArray<SpeciesId> UpdatedParents { get; } =
        [new("pret:SPECIES_RHYHORN"), new("pret:SPECIES_SLOWPOKE"), new("pret:SPECIES_GOLBAT"), new("pret:SPECIES_CHANSEY")];
}
