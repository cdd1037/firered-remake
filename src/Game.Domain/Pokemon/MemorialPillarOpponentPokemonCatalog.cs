using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class MemorialPillarOpponentPokemonCatalog
{
    public const string Profile = "firered-memorial-pillar-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_HOOTHOOT"), new("pret:SPECIES_NOCTOWL")];
}
