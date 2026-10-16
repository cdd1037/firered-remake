using Game.Domain.Encounters;
using Game.Domain.Ids;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private OrdinaryLandEncounterDefinition? OrdinaryLandDefinition(MapId map) =>
        _binding.OrdinaryLand.TryGetValue(map, out var definition) ? definition : null;

    private EarlyLandWildPokemonResult GenerateOrdinaryLand(CampaignState state,
        OrdinaryLandEncounterDefinition definition, EarlyLandEncounterState encounter) =>
        definition.GeneratePokemon(encounter, state.World.Rng, state.TrainerId);

    private EarlyLandEncounterState CompleteCurrentLandCandidate(EarlyLandEncounterState encounter, MapId map) =>
        EarlyLandEncounterRules.CompleteCandidate(encounter, _content.LandEncounters![map], OrdinaryLandDefinition(map));
}
