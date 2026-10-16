using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Original eleven first teams, including one Miu/Mia double, through shared source construction.</summary>
public static class SixIslandTrainerPartyFactory
{
    public static TrainerId DoubleTrainer { get; } = new("pret:TRAINER_TWINS_MIU_MIA");
    public static readonly ImmutableHashSet<TrainerId> TrainerIds = ImmutableHashSet.CreateRange<TrainerId>([
        new("pret:TRAINER_AROMA_LADY_ROSE"),
        new("pret:TRAINER_JUGGLER_EDWARD"),
        new("pret:TRAINER_SWIMMER_MALE_SAMIR"),
        new("pret:TRAINER_SWIMMER_FEMALE_DENISE"),
        new("pret:TRAINER_TWINS_MIU_MIA"),
        new("pret:TRAINER_HIKER_EARL"),
        new("pret:TRAINER_RUIN_MANIAC_STANLY"),
        new("pret:TRAINER_RUIN_MANIAC_FOSTER"),
        new("pret:TRAINER_RUIN_MANIAC_LARRY"),
        new("pret:TRAINER_HIKER_DARYL"),
        new("pret:TRAINER_POKEMANIAC_HECTOR")]);
    public static readonly ImmutableHashSet<string> BattleScripts = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "SixIsland_WaterPath_EventScript_Rose",
        "SixIsland_WaterPath_EventScript_Edward",
        "SixIsland_WaterPath_EventScript_Samir",
        "SixIsland_WaterPath_EventScript_Denise",
        "SixIsland_WaterPath_EventScript_Miu",
        "SixIsland_WaterPath_EventScript_Mia",
        "SixIsland_WaterPath_EventScript_Earl",
        "SixIsland_RuinValley_EventScript_Stanly",
        "SixIsland_RuinValley_EventScript_Foster",
        "SixIsland_RuinValley_EventScript_Larry",
        "SixIsland_RuinValley_EventScript_Daryl",
        "SixIsland_RuinValley_EventScript_Hector");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [new("pret:TRAINER_AROMA_LADY_ROSE"), new("pret:TRAINER_JUGGLER_EDWARD"), new("pret:TRAINER_SWIMMER_MALE_SAMIR"), new("pret:TRAINER_SWIMMER_FEMALE_DENISE"), new("pret:TRAINER_TWINS_MIU_MIA"), new("pret:TRAINER_HIKER_EARL"), new("pret:TRAINER_RUIN_MANIAC_STANLY"), new("pret:TRAINER_RUIN_MANIAC_FOSTER"), new("pret:TRAINER_RUIN_MANIAC_LARRY"), new("pret:TRAINER_HIKER_DARYL"), new("pret:TRAINER_POKEMANIAC_HECTOR")];
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = AllTrainers.Where(id => id != DoubleTrainer).ToImmutableArray();

    public static int BasePrize(SixIslandTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("six_island_trainer_id");
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }
    public static TrainerPartyCreationResult Create(SixIslandTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("six_island_trainer_catalog");
        foreach (var member in trainer.Party)
        {
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("six_island_trainer_species");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, opponents.Mechanics.Catalog, rng, opponents.Mechanics);
        if ((trainer.PartyFlags & 1) == 0) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE").Select(m => new PokemonMoveSlot(m,
                opponents.Mechanics.Catalog.Moves[m].Pp, opponents.Mechanics.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(SixIslandTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleScripts.Contains(battle.ScriptEntry))
            throw new NotSupportedException("six_island_trainer_battle_source");
    }
}
