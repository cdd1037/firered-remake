using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class FossilLabOwnedPokemonCatalog
{
    public const string Profile = "firered-fossil-lab-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_OMANYTE"), new("pret:SPECIES_OMASTAR"), new("pret:SPECIES_KABUTO"), new("pret:SPECIES_KABUTOPS"), new("pret:SPECIES_AERODACTYL")];
    public static SpeciesId AerodactylId => AdditionalSpecies[4];
}
