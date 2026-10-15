using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Original eleven first teams, including one Miu/Mia double, through shared source construction.</summary>
public static class SixIslandTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-six-island-trainers-v1";
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
    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv, int[] Encoded, string[] Moves, string HeldItem);
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle, int[] Encoded, string[] Items, ExpectedMember[] Party, int BasePrize);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_AROMA_LADY_ROSE")] = new("ROSE",129,1u,false,[204, 201, 205, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SUNKERN",191,"SUNKERN",49,0,[205, 207, 200, 197, 191, 204, 200],["pret:MOVE_ENDEAVOR", "pret:MOVE_SUNNY_DAY", "pret:MOVE_SYNTHESIS", "pret:MOVE_GIGA_DRAIN"],"ITEM_NONE"),new("pret:SPECIES_SUNFLORA",192,"SUNFLORA",49,0,[205, 207, 200, 192, 198, 201, 204, 187],["pret:MOVE_BULLET_SEED", "pret:MOVE_SUNNY_DAY", "pret:MOVE_PETAL_DANCE", "pret:MOVE_SOLAR_BEAM"],"ITEM_NONE")],1372),
        [new("pret:TRAINER_JUGGLER_EDWARD")] = new("EDWARD",11,1u,false,[191, 190, 209, 187, 204, 190],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_VOLTORB",100,"VOLTORB",46,0,[208, 201, 198, 206, 201, 204, 188],["pret:MOVE_SWIFT", "pret:MOVE_LIGHT_SCREEN", "pret:MOVE_SPARK", "pret:MOVE_SONIC_BOOM"],"ITEM_NONE"),new("pret:SPECIES_VOLTORB",100,"VOLTORB",46,0,[208, 201, 198, 206, 201, 204, 188],["pret:MOVE_SWIFT", "pret:MOVE_LIGHT_SCREEN", "pret:MOVE_SPARK", "pret:MOVE_SONIC_BOOM"],"ITEM_NONE"),new("pret:SPECIES_ELECTRODE",101,"ELECTRODE",47,0,[191, 198, 191, 189, 206, 204, 201, 190, 191],["pret:MOVE_SWIFT", "pret:MOVE_SPARK", "pret:MOVE_SELF_DESTRUCT", "pret:MOVE_SONIC_BOOM"],"ITEM_NONE"),new("pret:SPECIES_MR_MIME",122,"MR. MIME",48,0,[199, 204, 173, 0, 199, 195, 199, 191],["pret:MOVE_PSYCHIC", "pret:MOVE_ROLE_PLAY", "pret:MOVE_REFLECT", "pret:MOVE_ENCORE"],"ITEM_NONE")],1920),
        [new("pret:TRAINER_SWIMMER_MALE_SAMIR")] = new("SAMIR",8,1u,false,[205, 187, 199, 195, 204],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GYARADOS",130,"GYARADOS",50,0,[193, 211, 187, 204, 187, 190, 201, 205],["pret:MOVE_TWISTER", "pret:MOVE_HYDRO_PUMP", "pret:MOVE_RAIN_DANCE", "pret:MOVE_DRAGON_DANCE"],"ITEM_NONE")],200),
        [new("pret:TRAINER_SWIMMER_FEMALE_DENISE")] = new("DENISE",129,1u,false,[190, 191, 200, 195, 205, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_CHINCHOU",170,"CHINCHOU",49,0,[189, 194, 195, 200, 189, 194, 201, 207],["pret:MOVE_CONFUSE_RAY", "pret:MOVE_TAKE_DOWN", "pret:MOVE_HYDRO_PUMP", "pret:MOVE_CHARGE"],"ITEM_NONE"),new("pret:SPECIES_LANTURN",171,"LANTURN",49,0,[198, 187, 200, 206, 207, 204, 200],["pret:MOVE_WATER_GUN", "pret:MOVE_SPARK", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_TAKE_DOWN"],"ITEM_NONE")],196),
        [new("pret:TRAINER_TWINS_MIU_MIA")] = new("MIU & MIA",9,1u,true,[199, 195, 207, 0, 45, 0, 199, 195, 187],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_PIKACHU",25,"PIKACHU",50,0,[202, 195, 197, 187, 189, 194, 207],["pret:MOVE_THUNDERBOLT", "pret:MOVE_AGILITY", "pret:MOVE_THUNDER", "pret:MOVE_LIGHT_SCREEN"],"ITEM_NONE"),new("pret:SPECIES_PIKACHU",25,"PIKACHU",50,0,[202, 195, 197, 187, 189, 194, 207],["pret:MOVE_THUNDERBOLT", "pret:MOVE_AGILITY", "pret:MOVE_THUNDER", "pret:MOVE_LIGHT_SCREEN"],"ITEM_NONE")],1200),
        [new("pret:TRAINER_HIKER_EARL")] = new("EARL",11,1u,false,[191, 187, 204, 198],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_ONIX",95,"ONIX",49,0,[201, 200, 195, 210],["pret:MOVE_DRAGON_BREATH", "pret:MOVE_SANDSTORM", "pret:MOVE_SLAM", "pret:MOVE_IRON_TAIL"],"ITEM_NONE"),new("pret:SPECIES_MACHOKE",67,"MACHOKE",49,0,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION", "pret:MOVE_CROSS_CHOP"],"ITEM_NONE")],1764),
        [new("pret:TRAINER_RUIN_MANIAC_STANLY")] = new("STANLY",11,1u,false,[205, 206, 187, 200, 198, 211],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GRAVELER",75,"GRAVELER",48,0,[193, 204, 187, 208, 191, 198, 191, 204],["pret:MOVE_SELF_DESTRUCT", "pret:MOVE_ROLLOUT", "pret:MOVE_ROCK_BLAST", "pret:MOVE_EARTHQUAKE"],"ITEM_NONE"),new("pret:SPECIES_ONIX",95,"ONIX",48,0,[201, 200, 195, 210],["pret:MOVE_DRAGON_BREATH", "pret:MOVE_SANDSTORM", "pret:MOVE_SLAM", "pret:MOVE_IRON_TAIL"],"ITEM_NONE"),new("pret:SPECIES_GRAVELER",75,"GRAVELER",48,0,[193, 204, 187, 208, 191, 198, 191, 204],["pret:MOVE_SELF_DESTRUCT", "pret:MOVE_ROLLOUT", "pret:MOVE_ROCK_BLAST", "pret:MOVE_EARTHQUAKE"],"ITEM_NONE")],2304),
        [new("pret:TRAINER_RUIN_MANIAC_FOSTER")] = new("FOSTER",11,1u,false,[192, 201, 205, 206, 191, 204],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_GOLEM",76,"GOLEM",50,0,[193, 201, 198, 191, 199],["pret:MOVE_SELF_DESTRUCT", "pret:MOVE_ROLLOUT", "pret:MOVE_ROCK_BLAST", "pret:MOVE_EARTHQUAKE"],"ITEM_NONE")],2400),
        [new("pret:TRAINER_RUIN_MANIAC_LARRY")] = new("LARRY",11,1u,false,[198, 187, 204, 204, 211],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MACHOKE",67,"MACHOKE",49,0,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION", "pret:MOVE_CROSS_CHOP"],"ITEM_NONE"),new("pret:SPECIES_MACHOKE",67,"MACHOKE",49,0,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION", "pret:MOVE_CROSS_CHOP"],"ITEM_NONE")],2352),
        [new("pret:TRAINER_HIKER_DARYL")] = new("DARYL",11,1u,false,[190, 187, 204, 211, 198],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SUDOWOODO",185,"SUDOWOODO",50,0,[205, 207, 190, 201, 209, 201, 201, 190, 201],["pret:MOVE_ROCK_SLIDE", "pret:MOVE_BLOCK", "pret:MOVE_FAINT_ATTACK", "pret:MOVE_SLAM"],"ITEM_NONE")],1800),
        [new("pret:TRAINER_POKEMANIAC_HECTOR")] = new("HECTOR",3,1u,false,[194, 191, 189, 206, 201, 204],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_RHYHORN",111,"RHYHORN",49,30,[204, 194, 211, 194, 201, 204, 200],["pret:MOVE_SCARY_FACE", "pret:MOVE_ROCK_BLAST", "pret:MOVE_HORN_DRILL", "pret:MOVE_TAKE_DOWN"],"ITEM_NONE"),new("pret:SPECIES_KANGASKHAN",115,"KANGASKHAN",49,30,[197, 187, 200, 193, 187, 205, 197, 194, 187, 200],["pret:MOVE_RAGE", "pret:MOVE_ENDURE", "pret:MOVE_DIZZY_PUNCH", "pret:MOVE_REVERSAL"],"ITEM_NONE")],2352),
    }.ToImmutableDictionary();
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
            if (member.CustomMoves.Any(m => !opponents.Mechanics.Catalog.Moves.TryGetValue(m, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
                throw new NotSupportedException("six_island_trainer_move_source");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, opponents.Mechanics.Catalog, rng, opponents.Mechanics);
        if ((trainer.PartyFlags & 1) == 0) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.Where(m => m.Value != "pret:MOVE_NONE").Select(m => new PokemonMoveSlot(m,
                opponents.Mechanics.Catalog.Moves[m].Pp, opponents.Mechanics.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(SixIslandTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.BattlesByScript is null || catalog.Trainers.Count != 11 || catalog.BattlesByScript.Count != 12)
            throw new NotSupportedException("six_island_trainer_scope");
        foreach (var id in TrainerIds)
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Id != id ||
                trainer.Party.IsDefaultOrEmpty || trainer.Party.Any(m => m is null || m.CustomMoves.IsDefault || m.EncodedSpeciesName.IsDefaultOrEmpty) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefaultOrEmpty || trainer.AiFlagSymbols.IsDefault)
                throw new NotSupportedException("six_island_trainer_source");
        foreach (var script in BattleScripts)
            if (!catalog.BattlesByScript.TryGetValue(script, out var battle) || battle is null || battle.ScriptEntry != script)
                throw new NotSupportedException("six_island_trainer_battle_source");
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleScripts.Contains(battle.ScriptEntry))
            throw new NotSupportedException("six_island_trainer_battle_source");
    }
}
