using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Original first teams through shared source construction.</summary>
public static class SixIslandTrainerPartyFactory
{
    public static int BasePrize(SixIslandTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("six_island_trainer_id");
        return TrainerPartyFactory.BasePrize(trainer);
    }
    public static TrainerPartyCreationResult Create(SixIslandTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        return TrainerPartyFactory.Create(trainers.Trainers, opponents, id, rng);
    }
    public static void ValidateCatalog(SixIslandTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || string.IsNullOrWhiteSpace(battle.ScriptEntry) ||
            battle.Mode is not (0 or 4) || battle.Mode == 4 && string.IsNullOrWhiteSpace(battle.NotEnoughMonsText))
            throw new NotSupportedException("six_island_trainer_battle_source");
    }
}
