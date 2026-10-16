using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class LostCaveOwnedPokemonCatalog
{
    public const string Profile = "firered-lost-cave-owned-pokemon-v1";
    public const string OpponentProfile = "firered-lost-cave-opponents-v1";
    public static SpeciesId Murkrow { get; } = new("pret:SPECIES_MURKROW");
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } = [Murkrow];
}
