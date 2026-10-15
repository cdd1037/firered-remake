using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class SeviiRubyOwnedPokemonCatalog
{
    public const string Profile = "firered-sevii-ruby-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_RAPIDASH"), new("pret:SPECIES_SLUGMA"), new("pret:SPECIES_MAGCARGO")];
    public static SpeciesId Ponyta { get; } = new("pret:SPECIES_PONYTA");
}
