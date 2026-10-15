using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class FightingDojoOwnedPokemonCatalog
{
    public const string Profile = "firered-fighting-dojo-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_HITMONLEE"), new("pret:SPECIES_HITMONCHAN")];
}
