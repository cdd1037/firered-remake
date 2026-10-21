using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>Exact initial Ruby-region teams; shared source creation retains held items and double PID.</summary>
public static class SeviiRubyTrainerPartyFactory
{
    private static readonly ImmutableHashSet<string> BattleScripts = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "MtEmber_Exterior_EventScript_BattleGrunt1",
        "MtEmber_Exterior_EventScript_BattleGrunt2",
        "MtEmber_Exterior_EventScript_Beth",
        "MtEmber_Exterior_EventScript_Jocelyn",
        "MtEmber_Exterior_EventScript_Logan",
        "OneIsland_KindleRoad_EventScript_Abigail",
        "OneIsland_KindleRoad_EventScript_Bryce",
        "OneIsland_KindleRoad_EventScript_Claire",
        "OneIsland_KindleRoad_EventScript_Finn",
        "OneIsland_KindleRoad_EventScript_Garrett",
        "OneIsland_KindleRoad_EventScript_Hugh",
        "OneIsland_KindleRoad_EventScript_Kia",
        "OneIsland_KindleRoad_EventScript_Maria",
        "OneIsland_KindleRoad_EventScript_Mik",
        "OneIsland_KindleRoad_EventScript_Sharon",
        "OneIsland_KindleRoad_EventScript_Shea",
        "OneIsland_KindleRoad_EventScript_Tanya",
        "OneIsland_KindleRoad_EventScript_Tommy");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [
        new("pret:TRAINER_BLACK_BELT_HUGH"),
        new("pret:TRAINER_BLACK_BELT_SHEA"),
        new("pret:TRAINER_CAMPER_BRYCE"),
        new("pret:TRAINER_CRUSH_GIRL_JOCELYN"),
        new("pret:TRAINER_CRUSH_GIRL_SHARON"),
        new("pret:TRAINER_CRUSH_GIRL_TANYA"),
        new("pret:TRAINER_CRUSH_KIN_MIK_KIA"),
        new("pret:TRAINER_FISHERMAN_TOMMY"),
        new("pret:TRAINER_PICNICKER_CLAIRE"),
        new("pret:TRAINER_PKMN_RANGER_BETH"),
        new("pret:TRAINER_PKMN_RANGER_LOGAN"),
        new("pret:TRAINER_SWIMMER_FEMALE_ABIGAIL"),
        new("pret:TRAINER_SWIMMER_FEMALE_MARIA"),
        new("pret:TRAINER_SWIMMER_MALE_FINN"),
        new("pret:TRAINER_SWIMMER_MALE_GARRETT"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_43"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_44")];

    public static TrainerId DoubleTrainer { get; } = new("pret:TRAINER_CRUSH_KIN_MIK_KIA");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = AllTrainers.Where(id => id != DoubleTrainer).ToImmutableArray();

    public static int BasePrize(SeviiRubyTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("sevii_ruby_trainer_id");
        return TrainerPartyFactory.BasePrize(trainer);
    }

    public static TrainerPartyCreationResult Create(SeviiRubyTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        return TrainerPartyFactory.Create(trainers.Trainers, opponents, id, rng);
    }

    public static void ValidateCatalog(SeviiRubyTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, ValidateBattle);
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleScripts.Contains(battle.ScriptEntry))
            throw new NotSupportedException("sevii_ruby_trainer_battle_source");
    }
}
