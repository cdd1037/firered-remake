using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class FiveIslandOwnedPokemonCatalog
{
    public const string Profile = "firered-five-island-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_HOPPIP"), new("pret:SPECIES_SKIPLOOM"), new("pret:SPECIES_JUMPLUFF")];
}
