using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class StoneEvolutionOwnedPokemonCatalog
{
    public const string Profile = "firered-stone-evolution-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        new[] { "NINETALES", "ARCANINE", "POLIWRATH", "CLOYSTER", "EXEGGUTOR", "VAPOREON", "FLAREON" }
        .Select(name => new SpeciesId("pret:SPECIES_" + name)).ToImmutableArray();
}
