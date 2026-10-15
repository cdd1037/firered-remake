using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Pokemon;

// Declarative species groups used by encounter data. No ownership or authorization state.
public static class TrainerTowerOpponentPokemonCatalog
{
    public const string Profile = "firered-trainer-tower-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        new[] { "POLIWRATH", "VAPOREON", "FLAREON", "FERALIGATR", "LEDIAN", "TOGEPI", "AMPHAROS", "POLITOED",
            "ESPEON", "DUNSPARCE", "GRANBULL", "QWILFISH", "CORSOLA", "MANTINE", "PORYGON2", "SMOOCHUM", "MAGBY", "BLISSEY" }
        .Select(s => new SpeciesId("pret:SPECIES_" + s)).ToImmutableArray();
}
