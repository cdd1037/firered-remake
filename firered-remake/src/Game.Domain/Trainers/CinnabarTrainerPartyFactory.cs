using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Creates regional trainer parties from the shared definitions and canonical Pokemon mechanics.</summary>
public static class CinnabarTrainerPartyFactory
{
    public static TrainerId Blaine { get; } = new("pret:TRAINER_LEADER_BLAINE");

    private static readonly ImmutableHashSet<string> BattleKeys = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "CinnabarIsland_Gym_EventScript_Avery",
        "CinnabarIsland_Gym_EventScript_BattleAvery",
        "CinnabarIsland_Gym_EventScript_BattleDerek",
        "CinnabarIsland_Gym_EventScript_BattleDusty",
        "CinnabarIsland_Gym_EventScript_BattleQuinn",
        "CinnabarIsland_Gym_EventScript_BattleRamon",
        "CinnabarIsland_Gym_EventScript_BattleZac",
        "CinnabarIsland_Gym_EventScript_Blaine",
        "CinnabarIsland_Gym_EventScript_Derek",
        "CinnabarIsland_Gym_EventScript_Dusty",
        "CinnabarIsland_Gym_EventScript_Erik",
        "CinnabarIsland_Gym_EventScript_Quinn",
        "CinnabarIsland_Gym_EventScript_Ramon",
        "CinnabarIsland_Gym_EventScript_Zac",
        "PokemonMansion_1F_EventScript_Johnson",
        "PokemonMansion_1F_EventScript_Ted",
        "PokemonMansion_2F_EventScript_Arnie",
        "PokemonMansion_3F_EventScript_Braydon",
        "PokemonMansion_3F_EventScript_Simon",
        "PokemonMansion_B1F_EventScript_Ivan",
        "PokemonMansion_B1F_EventScript_Lewis",
        "Route21_North_EventScript_Ian",
        "Route21_North_EventScript_Lil",
        "Route21_North_EventScript_Ronald",
        "Route21_North_EventScript_Spencer",
        "Route21_North_EventScript_Wade",
        "Route21_South_EventScript_Claude",
        "Route21_South_EventScript_Jack",
        "Route21_South_EventScript_Jerome",
        "Route21_South_EventScript_Nolan",
        "Route21_South_EventScript_Roland");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [
        new("pret:TRAINER_BURGLAR_ARNIE"),
        new("pret:TRAINER_BURGLAR_DUSTY"),
        new("pret:TRAINER_BURGLAR_LEWIS"),
        new("pret:TRAINER_BURGLAR_QUINN"),
        new("pret:TRAINER_BURGLAR_RAMON"),
        new("pret:TRAINER_BURGLAR_SIMON"),
        new("pret:TRAINER_FISHERMAN_CLAUDE"),
        new("pret:TRAINER_FISHERMAN_NOLAN"),
        new("pret:TRAINER_FISHERMAN_RONALD"),
        new("pret:TRAINER_FISHERMAN_WADE"),
        new("pret:TRAINER_LEADER_BLAINE"),
        new("pret:TRAINER_SCIENTIST_BRAYDON"),
        new("pret:TRAINER_SCIENTIST_IVAN"),
        new("pret:TRAINER_SCIENTIST_TED"),
        new("pret:TRAINER_SIS_AND_BRO_LIL_IAN"),
        new("pret:TRAINER_SUPER_NERD_AVERY"),
        new("pret:TRAINER_SUPER_NERD_DEREK"),
        new("pret:TRAINER_SUPER_NERD_ERIK"),
        new("pret:TRAINER_SUPER_NERD_ZAC"),
        new("pret:TRAINER_SWIMMER_MALE_JACK"),
        new("pret:TRAINER_SWIMMER_MALE_JEROME"),
        new("pret:TRAINER_SWIMMER_MALE_ROLAND"),
        new("pret:TRAINER_SWIMMER_MALE_SPENCER"),
        new("pret:TRAINER_YOUNGSTER_JOHNSON")
    ];
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = AllTrainers.Where(t => t != CinnabarDoubleTrainerEncounter.TrainerId).ToImmutableArray();
    public static ImmutableArray<TrainerId> GymTrainers { get; } = [
        new("pret:TRAINER_BURGLAR_DUSTY"),
        new("pret:TRAINER_BURGLAR_QUINN"),
        new("pret:TRAINER_BURGLAR_RAMON"),
        new("pret:TRAINER_LEADER_BLAINE"),
        new("pret:TRAINER_SUPER_NERD_AVERY"),
        new("pret:TRAINER_SUPER_NERD_DEREK"),
        new("pret:TRAINER_SUPER_NERD_ERIK"),
        new("pret:TRAINER_SUPER_NERD_ZAC")
    ];
    public static TrainerPartyCreationResult Create(CinnabarTrainerCatalog trainers, GameOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("cinnabar_trainer_catalog");
        var source = opponents.Mechanics;
        foreach (var member in trainer.Party)
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("cinnabar_trainer_species");
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        if ((trainer.PartyFlags & 1) == 0) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(CinnabarTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleKeys.Contains(battle.ScriptEntry))
            throw new NotSupportedException("cinnabar_trainer_battle_source");
    }
}
