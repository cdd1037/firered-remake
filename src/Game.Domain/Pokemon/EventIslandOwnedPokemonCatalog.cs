using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class EventIslandOwnedPokemonCatalog
{
    public const string Profile = "firered-event-island-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_HO_OH"), new("pret:SPECIES_LUGIA"), new("pret:SPECIES_DEOXYS")];
}
