using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>Original Grunt45 no-item/default-move party through the shared source factory.</summary>
public static class FourIslandTrainerPartyFactory
{
    public const string BattleScript = "FourIsland_IcefallCave_Back_EventScript_LoreleiRocketsScene";
    public static TrainerId Grunt45 { get; } = new("pret:TRAINER_TEAM_ROCKET_GRUNT_45");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [Grunt45];
    public static ImmutableArray<TrainerId> SupportedTrainers => AllTrainers;

    public static int BasePrize(FourIslandTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (id != Grunt45) throw new NotSupportedException("four_island_trainer_id");
        var trainer = trainers.Trainers[id];
        return checked(4 * trainer.BaseMoney * trainer.Party[^1].Level);
    }
    public static TrainerPartyCreationResult Create(FourIslandTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        return TrainerPartyFactory.Create(trainers.Trainers, opponents, id, rng);
    }
    public static void ValidateCatalog(FourIslandTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || battle.ScriptEntry != BattleScript)
            throw new NotSupportedException("four_island_trainer_battle_source");
    }
}
