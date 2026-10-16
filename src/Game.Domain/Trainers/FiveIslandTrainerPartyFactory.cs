using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Nine original singles with source admin medicines and twenty-six authentic party members.</summary>
public static class FiveIslandTrainerPartyFactory
{
    public static readonly ImmutableHashSet<TrainerId> TrainerIds = ImmutableHashSet.CreateRange<TrainerId>([
        new("pret:TRAINER_TEAM_ROCKET_ADMIN"),
        new("pret:TRAINER_TEAM_ROCKET_ADMIN_2"),
        new("pret:TRAINER_SCIENTIST_GIDEON"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_42"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_47"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_48"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_49"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_50"),
        new("pret:TRAINER_TEAM_ROCKET_GRUNT_51")]);
    public static readonly ImmutableHashSet<string> BattleScripts = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "FiveIsland_Meadow_EventScript_Rocket3",
        "FiveIsland_Meadow_EventScript_Rocket1",
        "FiveIsland_Meadow_EventScript_Rocket2",
        "FiveIsland_RocketWarehouse_EventScript_Grunt2",
        "FiveIsland_RocketWarehouse_EventScript_Grunt3",
        "FiveIsland_RocketWarehouse_EventScript_Admin1",
        "FiveIsland_RocketWarehouse_EventScript_Admin2",
        "FiveIsland_RocketWarehouse_EventScript_Grunt1",
        "FiveIsland_RocketWarehouse_EventScript_Gideon");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [new("pret:TRAINER_TEAM_ROCKET_ADMIN"), new("pret:TRAINER_TEAM_ROCKET_ADMIN_2"), new("pret:TRAINER_SCIENTIST_GIDEON"), new("pret:TRAINER_TEAM_ROCKET_GRUNT_42"), new("pret:TRAINER_TEAM_ROCKET_GRUNT_47"), new("pret:TRAINER_TEAM_ROCKET_GRUNT_48"), new("pret:TRAINER_TEAM_ROCKET_GRUNT_49"), new("pret:TRAINER_TEAM_ROCKET_GRUNT_50"), new("pret:TRAINER_TEAM_ROCKET_GRUNT_51")];
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = AllTrainers;

    public static int BasePrize(FiveIslandTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("five_island_trainer_id");
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }
    public static TrainerPartyCreationResult Create(FiveIslandTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("five_island_trainer_catalog");
        foreach (var member in trainer.Party)
        {
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("five_island_trainer_species");
            if (member.CustomMoves.Any(m => !opponents.Mechanics.Catalog.Moves.TryGetValue(m, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
                throw new NotSupportedException("five_island_trainer_move_source");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, opponents.Mechanics.Catalog, rng, opponents.Mechanics);
        if ((trainer.PartyFlags & 1) == 0) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.Where(m => m.Value != "pret:MOVE_NONE").Select(m => new PokemonMoveSlot(m,
                opponents.Mechanics.Catalog.Moves[m].Pp, opponents.Mechanics.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(FiveIslandTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleScripts.Contains(battle.ScriptEntry))
            throw new NotSupportedException("five_island_trainer_battle_source");
    }
}
