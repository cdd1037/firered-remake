using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

public sealed record CyclingRoadTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<string, CeruleanTrainerBattleDefinition> BattlesByScript);

public static class CyclingRoadTrainerPartyFactory
{
    public static void ValidateCatalog(CyclingRoadTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, ValidateBattle);
        if (catalog.BattlesByScript.Values.Any(b => b.Mode != 0 || b.BattleTypeFlags != 8 ||
            b.Source.Path != "data/scripts/trainers.inc" || b.Trainer != catalog.Trainers[b.Trainer].Id))
            throw new ArgumentException("cycling_road_trainer_definition");
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !(battle.ScriptEntry.StartsWith("Route16_EventScript_", StringComparison.Ordinal) ||
            battle.ScriptEntry.StartsWith("Route17_EventScript_", StringComparison.Ordinal) ||
            battle.ScriptEntry.StartsWith("Route18_EventScript_", StringComparison.Ordinal)))
            throw new ArgumentException("cycling_road_trainer_script");
    }

    public static TrainerPartyCreationResult Create(CyclingRoadTrainerCatalog trainers,
        GamePokemonCatalog owned, TrainerId id, RngState rng)
    {
        if (owned is null || !trainers.Trainers.TryGetValue(id, out var trainer) ||
            trainer.Party.Any(p => !owned.Opponents.Catalog.Species.ContainsKey(p.Species)))
            throw new ArgumentException("cycling_road_trainer_opponent");
        return TrainerPartyFactory.CreateValidated(trainer, owned.Mechanics.Catalog, rng, owned.Mechanics);
    }
}
