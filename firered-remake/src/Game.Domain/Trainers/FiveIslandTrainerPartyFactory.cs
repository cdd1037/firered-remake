using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Nine original singles with source admin medicines and twenty-six authentic party members.</summary>
public static class FiveIslandTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-five-island-trainers-v1";
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
    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv, int[] Encoded, string[] Moves, string HeldItem);
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle, int[] Encoded, string[] Items, ExpectedMember[] Party, int BasePrize);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_TEAM_ROCKET_ADMIN")] = new("ADMIN",6,7u,false,[187, 190, 199, 195, 200],["ITEM_HYPER_POTION", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MUK",89,"MUK",52,150,[199, 207, 197],["pret:MOVE_SLUDGE_BOMB", "pret:MOVE_SCREECH", "pret:MOVE_MINIMIZE", "pret:MOVE_ROCK_TOMB"],"ITEM_NONE"),new("pret:SPECIES_ARBOK",24,"ARBOK",53,150,[187, 204, 188, 201, 197],["pret:MOVE_SLUDGE_BOMB", "pret:MOVE_BITE", "pret:MOVE_EARTHQUAKE", "pret:MOVE_IRON_TAIL"],"ITEM_NONE"),new("pret:SPECIES_VILEPLUME",45,"VILEPLUME",54,150,[208, 195, 198, 191, 202, 198, 207, 199, 191],["pret:MOVE_SLUDGE_BOMB", "pret:MOVE_GIGA_DRAIN", "pret:MOVE_SLEEP_POWDER", "pret:MOVE_STUN_SPORE"],"ITEM_NONE")],1728),
        [new("pret:TRAINER_TEAM_ROCKET_ADMIN_2")] = new("ADMIN",6,7u,false,[187, 190, 199, 195, 200],["ITEM_HYPER_POTION", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GOLBAT",42,"GOLBAT",53,200,[193, 201, 198, 188, 187, 206],["pret:MOVE_CONFUSE_RAY", "pret:MOVE_SLUDGE_BOMB", "pret:MOVE_AIR_CUTTER", "pret:MOVE_SHADOW_BALL"],"ITEM_NONE"),new("pret:SPECIES_WEEZING",110,"WEEZING",54,200,[209, 191, 191, 212, 195, 200, 193],["pret:MOVE_SLUDGE_BOMB", "pret:MOVE_THUNDERBOLT", "pret:MOVE_EXPLOSION", "pret:MOVE_SHADOW_BALL"],"ITEM_NONE"),new("pret:SPECIES_HOUNDOOM",229,"HOUNDOOM",55,200,[194, 201, 207, 200, 190, 201, 201, 199],["pret:MOVE_FLAMETHROWER", "pret:MOVE_CRUNCH", "pret:MOVE_IRON_TAIL", "pret:MOVE_SHADOW_BALL"],"ITEM_NONE")],1760),
        [new("pret:TRAINER_SCIENTIST_GIDEON")] = new("GIDEON",3,1u,false,[193, 195, 190, 191, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_VOLTORB",100,"VOLTORB",46,0,[208, 201, 198, 206, 201, 204, 188],["pret:MOVE_SWIFT", "pret:MOVE_SCREECH", "pret:MOVE_SPARK", "pret:MOVE_SONIC_BOOM"],"ITEM_NONE"),new("pret:SPECIES_ELECTRODE",101,"ELECTRODE",46,0,[191, 198, 191, 189, 206, 204, 201, 190, 191],["pret:MOVE_SPARK", "pret:MOVE_SONIC_BOOM", "pret:MOVE_SCREECH", "pret:MOVE_CHARGE"],"ITEM_NONE"),new("pret:SPECIES_MAGNEMITE",81,"MAGNEMITE",46,0,[199, 187, 193, 200, 191, 199, 195, 206, 191],["pret:MOVE_SCREECH", "pret:MOVE_SWIFT", "pret:MOVE_SPARK", "pret:MOVE_THUNDER_WAVE"],"ITEM_NONE"),new("pret:SPECIES_MAGNETON",82,"MAGNETON",46,0,[199, 187, 193, 200, 191, 206, 201, 200],["pret:MOVE_TRI_ATTACK", "pret:MOVE_SPARK", "pret:MOVE_THUNDER_WAVE", "pret:MOVE_SONIC_BOOM"],"ITEM_NONE"),new("pret:SPECIES_PORYGON",137,"PORYGON",46,0,[202, 201, 204, 211, 193, 201, 200],["pret:MOVE_TRI_ATTACK", "pret:MOVE_CONVERSION", "pret:MOVE_RECOVER", "pret:MOVE_PSYBEAM"],"ITEM_NONE")],2208),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_42")] = new("GRUNT",6,1u,false,[193, 204, 207, 200, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_HOUNDOUR",228,"HOUNDOUR",49,0,[194, 201, 207, 200, 190, 201, 207, 204],["pret:MOVE_ODOR_SLEUTH", "pret:MOVE_FAINT_ATTACK", "pret:MOVE_FLAMETHROWER", "pret:MOVE_CRUNCH"],"ITEM_NONE"),new("pret:SPECIES_HOUNDOUR",228,"HOUNDOUR",49,0,[194, 201, 207, 200, 190, 201, 207, 204],["pret:MOVE_ODOR_SLEUTH", "pret:MOVE_FAINT_ATTACK", "pret:MOVE_FLAMETHROWER", "pret:MOVE_CRUNCH"],"ITEM_NONE")],1568),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_47")] = new("GRUNT",6,1u,false,[193, 204, 207, 200, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MACHOP",66,"MACHOP",48,0,[199, 187, 189, 194, 201, 202],["pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION", "pret:MOVE_CROSS_CHOP", "pret:MOVE_SCARY_FACE"],"ITEM_NONE"),new("pret:SPECIES_MACHOP",66,"MACHOP",48,0,[199, 187, 189, 194, 201, 202],["pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION", "pret:MOVE_CROSS_CHOP", "pret:MOVE_SCARY_FACE"],"ITEM_NONE"),new("pret:SPECIES_MACHOKE",67,"MACHOKE",48,0,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION", "pret:MOVE_CROSS_CHOP"],"ITEM_NONE")],1536),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_48")] = new("GRUNT",6,1u,false,[193, 204, 207, 200, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_HYPNO",97,"HYPNO",49,0,[194, 211, 202, 200, 201],["pret:MOVE_MEDITATE", "pret:MOVE_PSYCHIC", "pret:MOVE_PSYCH_UP", "pret:MOVE_SWAGGER"],"ITEM_NONE"),new("pret:SPECIES_HYPNO",97,"HYPNO",49,0,[194, 211, 202, 200, 201],["pret:MOVE_MEDITATE", "pret:MOVE_PSYCHIC", "pret:MOVE_PSYCH_UP", "pret:MOVE_SWAGGER"],"ITEM_NONE")],1568),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_49")] = new("GRUNT",6,1u,false,[193, 204, 207, 200, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_RATTATA",19,"RATTATA",48,0,[204, 187, 206, 206, 187, 206, 187],["pret:MOVE_FOCUS_ENERGY", "pret:MOVE_PURSUIT", "pret:MOVE_SUPER_FANG", "pret:MOVE_ENDEAVOR"],"ITEM_NONE"),new("pret:SPECIES_GRIMER",88,"GRIMER",48,0,[193, 204, 195, 199, 191, 204],["pret:MOVE_MINIMIZE", "pret:MOVE_SCREECH", "pret:MOVE_ACID_ARMOR", "pret:MOVE_SLUDGE_BOMB"],"ITEM_NONE"),new("pret:SPECIES_MUK",89,"MUK",48,0,[199, 207, 197],["pret:MOVE_MINIMIZE", "pret:MOVE_SCREECH", "pret:MOVE_ACID_ARMOR", "pret:MOVE_SLUDGE_BOMB"],"ITEM_NONE")],1536),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_50")] = new("GRUNT",6,1u,false,[193, 204, 207, 200, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_KOFFING",109,"KOFFING",49,0,[197, 201, 192, 192, 195, 200, 193],["pret:MOVE_MEMENTO", "pret:MOVE_HAZE", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SLUDGE"],"ITEM_NONE"),new("pret:SPECIES_WEEZING",110,"WEEZING",49,0,[209, 191, 191, 212, 195, 200, 193],["pret:MOVE_HAZE", "pret:MOVE_SMOKESCREEN", "pret:MOVE_SLUDGE", "pret:MOVE_SELF_DESTRUCT"],"ITEM_NONE")],1568),
        [new("pret:TRAINER_TEAM_ROCKET_GRUNT_51")] = new("GRUNT",6,1u,false,[193, 204, 207, 200, 206],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_EKANS",23,"EKANS",48,50,[191, 197, 187, 200, 205],["pret:MOVE_STOCKPILE", "pret:MOVE_SWALLOW", "pret:MOVE_SPIT_UP", "pret:MOVE_HAZE"],"ITEM_NONE"),new("pret:SPECIES_GLOOM",44,"GLOOM",48,50,[193, 198, 201, 201, 199],["pret:MOVE_SLEEP_POWDER", "pret:MOVE_ACID", "pret:MOVE_MOONLIGHT", "pret:MOVE_PETAL_DANCE"],"ITEM_NONE"),new("pret:SPECIES_GLOOM",44,"GLOOM",48,50,[193, 198, 201, 201, 199],["pret:MOVE_SLEEP_POWDER", "pret:MOVE_ACID", "pret:MOVE_MOONLIGHT", "pret:MOVE_PETAL_DANCE"],"ITEM_NONE")],1536),
    }.ToImmutableDictionary();
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
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.BattlesByScript is null || catalog.Trainers.Count != 9 || catalog.BattlesByScript.Count != 9)
            throw new NotSupportedException("five_island_trainer_scope");
        foreach (var id in TrainerIds)
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Id != id ||
                trainer.Party.IsDefaultOrEmpty || trainer.Party.Any(m => m is null || m.CustomMoves.IsDefault || m.EncodedSpeciesName.IsDefaultOrEmpty) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefaultOrEmpty || trainer.AiFlagSymbols.IsDefault)
                throw new NotSupportedException("five_island_trainer_source");
        foreach (var script in BattleScripts)
            if (!catalog.BattlesByScript.TryGetValue(script, out var battle) || battle is null || battle.ScriptEntry != script)
                throw new NotSupportedException("five_island_trainer_battle_source");
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleScripts.Contains(battle.ScriptEntry))
            throw new NotSupportedException("five_island_trainer_battle_source");
    }
}
