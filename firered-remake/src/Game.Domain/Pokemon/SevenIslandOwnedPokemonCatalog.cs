using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class SevenIslandOwnedPokemonCatalog
{
    public const string Profile = "firered-seven-island-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_PHANPY"), new("pret:SPECIES_DONPHAN"), new("pret:SPECIES_LARVITAR"), new("pret:SPECIES_PUPITAR"), new("pret:SPECIES_TYRANITAR"), new("pret:SPECIES_SKARMORY"), new("pret:SPECIES_UNOWN")];
}
