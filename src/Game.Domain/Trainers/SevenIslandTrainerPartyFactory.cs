using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.Trainers;

/// <summary>Exact original singles and preset doubles through the existing trainer construction kernel.</summary>
public static class SevenIslandTrainerPartyFactory
{
    public static readonly ImmutableHashSet<string> BattleScripts = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "SevenIsland_SevaultCanyon_EventScript_Cyndy",
        "SevenIsland_SevaultCanyon_EventScript_Evan",
        "SevenIsland_SevaultCanyon_EventScript_Jackson",
        "SevenIsland_SevaultCanyon_EventScript_Katelyn",
        "SevenIsland_SevaultCanyon_EventScript_Leroy",
        "SevenIsland_SevaultCanyon_EventScript_Michelle",
        "SevenIsland_SevaultCanyon_EventScript_Lex",
        "SevenIsland_SevaultCanyon_EventScript_Nya",
        "SevenIsland_SevaultCanyon_Entrance_EventScript_Miah",
        "SevenIsland_SevaultCanyon_Entrance_EventScript_Eve",
        "SevenIsland_SevaultCanyon_Entrance_EventScript_Jon",
        "SevenIsland_SevaultCanyon_Entrance_EventScript_Nicolas",
        "SevenIsland_SevaultCanyon_Entrance_EventScript_Madeline",
        "SevenIsland_SevaultCanyon_Entrance_EventScript_Mason",
        "SevenIsland_TanobyRuins_EventScript_Brandon",
        "SevenIsland_TanobyRuins_EventScript_Benjamin",
        "SevenIsland_TanobyRuins_EventScript_Edna",
        "SevenIsland_TanobyRuins_EventScript_Clifford",
        "SevenIsland_TrainerTower_EventScript_Dario",
        "SevenIsland_TrainerTower_EventScript_Rodette");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [
        new("pret:TRAINER_CRUSH_GIRL_CYNDY"),
        new("pret:TRAINER_TAMER_EVAN"),
        new("pret:TRAINER_PKMN_RANGER_JACKSON"),
        new("pret:TRAINER_PKMN_RANGER_KATELYN"),
        new("pret:TRAINER_COOLTRAINER_LEROY"),
        new("pret:TRAINER_COOLTRAINER_MICHELLE"),
        new("pret:TRAINER_COOL_COUPLE_LEX_NYA"),
        new("pret:TRAINER_AROMA_LADY_MIAH"),
        new("pret:TRAINER_YOUNG_COUPLE_EVE_JON"),
        new("pret:TRAINER_PKMN_RANGER_NICOLAS"),
        new("pret:TRAINER_PKMN_RANGER_MADELINE"),
        new("pret:TRAINER_JUGGLER_MASON"),
        new("pret:TRAINER_RUIN_MANIAC_BRANDON"),
        new("pret:TRAINER_RUIN_MANIAC_BENJAMIN"),
        new("pret:TRAINER_PAINTER_EDNA"),
        new("pret:TRAINER_GENTLEMAN_CLIFFORD"),
        new("pret:TRAINER_PSYCHIC_DARIO"),
        new("pret:TRAINER_PSYCHIC_RODETTE")];
    public static readonly ImmutableHashSet<TrainerId> TrainerIds = AllTrainers.ToImmutableHashSet();
    public static ImmutableArray<TrainerId> SupportedTrainers => AllTrainers;
    public static int BasePrize(SevenIslandTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("seven_island_trainer_id");
        return TrainerPartyFactory.BasePrize(trainer);
    }
    public static TrainerPartyCreationResult Create(SevenIslandTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        return TrainerPartyFactory.Create(trainers.Trainers, opponents, id, rng);
    }
    public static void ValidateCatalog(SevenIslandTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleScripts.Contains(battle.ScriptEntry))
            throw new NotSupportedException("seven_island_trainer_battle_source");
    }
}
