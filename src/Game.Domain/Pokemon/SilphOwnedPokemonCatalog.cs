using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class SilphOwnedPokemonCatalog
{
    public const string Profile = "firered-silph-owned-pokemon-v1";
    public static SpeciesId Lapras { get; } = new("pret:SPECIES_LAPRAS");
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } = [Lapras];
}
