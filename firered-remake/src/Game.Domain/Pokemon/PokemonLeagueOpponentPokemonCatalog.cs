using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class PokemonLeagueOpponentPokemonCatalog
{
    public const string Profile = "firered-pokemon-league-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_MACHAMP"), new("pret:SPECIES_GENGAR"), new("pret:SPECIES_HITMONLEE"), new("pret:SPECIES_HITMONCHAN"), new("pret:SPECIES_RHYDON"), new("pret:SPECIES_JYNX"), new("pret:SPECIES_AERODACTYL"), new("pret:SPECIES_DRAGONAIR"), new("pret:SPECIES_DRAGONITE")];
}
