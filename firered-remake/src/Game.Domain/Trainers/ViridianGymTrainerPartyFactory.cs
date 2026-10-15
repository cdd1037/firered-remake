using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Pinned nine-team, 29-member gym roster. Default moves and held items are independent bits.</summary>
public static class ViridianGymTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-viridian-gym-trainers-v1";
    public static TrainerId Giovanni { get; } = new("pret:TRAINER_LEADER_GIOVANNI");
    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv, int[] Encoded, string[] Moves, string HeldItem);
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle, int[] Encoded, string[] Items, ExpectedMember[] Party,
        int BasePrize);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [new("pret:TRAINER_TAMER_JASON")] = new("JASON",11,1u,false,[196, 187, 205, 201, 200],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_RHYHORN",111,"RHYHORN",43,40,[204, 194, 211, 194, 201, 204, 200],["pret:MOVE_SCARY_FACE", "pret:MOVE_ROCK_BLAST", "pret:MOVE_HORN_DRILL", "pret:MOVE_TAKE_DOWN"],"ITEM_NONE")],1720),
        [new("pret:TRAINER_TAMER_COLE")] = new("COLE",11,1u,false,[189, 201, 198, 191],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_ARBOK",24,"ARBOK",39,40,[187, 204, 188, 201, 197],["pret:MOVE_BITE", "pret:MOVE_GLARE", "pret:MOVE_SCREECH", "pret:MOVE_ACID"],"ITEM_NONE"),new("pret:SPECIES_TAUROS",128,"TAUROS",39,40,[206, 187, 207, 204, 201, 205],["pret:MOVE_SCARY_FACE", "pret:MOVE_PURSUIT", "pret:MOVE_SWAGGER", "pret:MOVE_REST"],"ITEM_NONE")],1560),
        [new("pret:TRAINER_BLACK_BELT_ATSUSHI")] = new("ATSUSHI",4,1u,false,[187, 206, 205, 207, 205, 194, 195],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MACHOP",66,"MACHOP",40,100,[199, 187, 189, 194, 201, 202],["pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION", "pret:MOVE_CROSS_CHOP"],"ITEM_BLACK_BELT"),new("pret:SPECIES_MACHOKE",67,"MACHOKE",40,100,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_SEISMIC_TOSS", "pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW"],"ITEM_BLACK_BELT")],960),
        [new("pret:TRAINER_BLACK_BELT_KIYO")] = new("KIYO",4,1u,false,[197, 195, 211, 201],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MACHOKE",67,"MACHOKE",43,100,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION"],"ITEM_BLACK_BELT")],1032),
        [new("pret:TRAINER_BLACK_BELT_TAKASHI")] = new("TAKASHI",4,1u,false,[206, 187, 197, 187, 205, 194, 195],["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MACHOKE",67,"MACHOKE",38,100,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_SEISMIC_TOSS", "pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW"],"ITEM_BLACK_BELT"),new("pret:SPECIES_MACHOP",66,"MACHOP",38,100,[199, 187, 189, 194, 201, 202],["pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW", "pret:MOVE_SUBMISSION"],"ITEM_BLACK_BELT"),new("pret:SPECIES_MACHOKE",67,"MACHOKE",38,100,[199, 187, 189, 194, 201, 197, 191],["pret:MOVE_SEISMIC_TOSS", "pret:MOVE_FORESIGHT", "pret:MOVE_REVENGE", "pret:MOVE_VITAL_THROW"],"ITEM_BLACK_BELT")],912),
        [new("pret:TRAINER_COOLTRAINER_SAMUEL")] = new("SAMUEL",5,7u,false,[205, 187, 199, 207, 191, 198],["ITEM_SUPER_POTION", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SANDSLASH",28,"SANDSLASH",37,100,[205, 187, 200, 190, 205, 198, 187, 205, 194],["pret:MOVE_SLASH", "pret:MOVE_SWIFT", "pret:MOVE_SAND_ATTACK", "pret:MOVE_POISON_STING"],"ITEM_NONE"),new("pret:SPECIES_SANDSLASH",28,"SANDSLASH",37,100,[205, 187, 200, 190, 205, 198, 187, 205, 194],["pret:MOVE_SLASH", "pret:MOVE_SWIFT", "pret:MOVE_SAND_ATTACK", "pret:MOVE_POISON_STING"],"ITEM_NONE"),new("pret:SPECIES_RHYHORN",111,"RHYHORN",38,100,[204, 194, 211, 194, 201, 204, 200],["pret:MOVE_TAKE_DOWN", "pret:MOVE_ROCK_BLAST", "pret:MOVE_FURY_ATTACK", "pret:MOVE_SCARY_FACE"],"ITEM_NONE"),new("pret:SPECIES_NIDORINO",33,"NIDORINO",39,100,[200, 195, 190, 201, 204, 195, 200, 201],["pret:MOVE_FURY_ATTACK", "pret:MOVE_HORN_ATTACK", "pret:MOVE_POISON_STING", "pret:MOVE_DOUBLE_KICK"],"ITEM_NONE"),new("pret:SPECIES_NIDOKING",34,"NIDOKING",39,100,[200, 195, 190, 201, 197, 195, 200, 193],["pret:MOVE_THRASH", "pret:MOVE_DOUBLE_KICK", "pret:MOVE_POISON_STING", "pret:MOVE_FOCUS_ENERGY"],"ITEM_NONE")],1404),
        [new("pret:TRAINER_COOLTRAINER_YUJI")] = new("YUJI",5,7u,false,[211, 207, 196, 195],["ITEM_HYPER_POTION", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_SANDSLASH",28,"SANDSLASH",38,100,[205, 187, 200, 190, 205, 198, 187, 205, 194],["pret:MOVE_SLASH", "pret:MOVE_SWIFT", "pret:MOVE_SAND_ATTACK", "pret:MOVE_POISON_STING"],"ITEM_NONE"),new("pret:SPECIES_GRAVELER",75,"GRAVELER",38,100,[193, 204, 187, 208, 191, 198, 191, 204],["pret:MOVE_ROCK_BLAST", "pret:MOVE_MAGNITUDE", "pret:MOVE_MUD_SPORT", "pret:MOVE_DEFENSE_CURL"],"ITEM_NONE"),new("pret:SPECIES_ONIX",95,"ONIX",38,100,[201, 200, 195, 210],["pret:MOVE_DRAGON_BREATH", "pret:MOVE_SANDSTORM", "pret:MOVE_ROCK_THROW", "pret:MOVE_BIND"],"ITEM_NONE"),new("pret:SPECIES_GRAVELER",75,"GRAVELER",38,100,[193, 204, 187, 208, 191, 198, 191, 204],["pret:MOVE_ROCK_BLAST", "pret:MOVE_MAGNITUDE", "pret:MOVE_ROLLOUT", "pret:MOVE_DEFENSE_CURL"],"ITEM_NONE"),new("pret:SPECIES_MAROWAK",105,"MAROWAK",38,100,[199, 187, 204, 201, 209, 187, 197],["pret:MOVE_BONEMERANG", "pret:MOVE_HEADBUTT", "pret:MOVE_LEER", "pret:MOVE_GROWL"],"ITEM_NONE")],1368),
        [new("pret:TRAINER_COOLTRAINER_WARREN")] = new("WARREN",5,7u,false,[209, 187, 204, 204, 191, 200],["ITEM_HYPER_POTION", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"],[new("pret:SPECIES_MAROWAK",105,"MAROWAK",37,100,[199, 187, 204, 201, 209, 187, 197],["pret:MOVE_BONEMERANG", "pret:MOVE_HEADBUTT", "pret:MOVE_LEER", "pret:MOVE_GROWL"],"ITEM_NONE"),new("pret:SPECIES_MAROWAK",105,"MAROWAK",37,100,[199, 187, 204, 201, 209, 187, 197],["pret:MOVE_BONEMERANG", "pret:MOVE_HEADBUTT", "pret:MOVE_LEER", "pret:MOVE_GROWL"],"ITEM_NONE"),new("pret:SPECIES_RHYHORN",111,"RHYHORN",38,100,[204, 194, 211, 194, 201, 204, 200],["pret:MOVE_TAKE_DOWN", "pret:MOVE_ROCK_BLAST", "pret:MOVE_FURY_ATTACK", "pret:MOVE_SCARY_FACE"],"ITEM_NONE"),new("pret:SPECIES_NIDORINA",30,"NIDORINA",39,100,[200, 195, 190, 201, 204, 195, 200, 187],["pret:MOVE_FURY_SWIPES", "pret:MOVE_BITE", "pret:MOVE_POISON_STING", "pret:MOVE_DOUBLE_KICK"],"ITEM_NONE"),new("pret:SPECIES_NIDOQUEEN",31,"NIDOQUEEN",39,100,[200, 195, 190, 201, 203, 207, 191, 191, 200],["pret:MOVE_BODY_SLAM", "pret:MOVE_BITE", "pret:MOVE_POISON_STING", "pret:MOVE_DOUBLE_KICK"],"ITEM_NONE")],1404),
        [new("pret:TRAINER_LEADER_GIOVANNI")] = new("GIOVANNI",6,7u,false,[193, 195, 201, 208, 187, 200, 200, 195],["ITEM_HYPER_POTION", "ITEM_HYPER_POTION", "ITEM_FULL_HEAL", "ITEM_NONE"],[new("pret:SPECIES_RHYHORN",111,"RHYHORN",45,0,[204, 194, 211, 194, 201, 204, 200],["pret:MOVE_TAKE_DOWN", "pret:MOVE_ROCK_BLAST", "pret:MOVE_SCARY_FACE", "pret:MOVE_EARTHQUAKE"],"ITEM_NONE"),new("pret:SPECIES_DUGTRIO",51,"DUGTRIO",42,0,[190, 207, 193, 206, 204, 195, 201],["pret:MOVE_SLASH", "pret:MOVE_SAND_TOMB", "pret:MOVE_MUD_SLAP", "pret:MOVE_EARTHQUAKE"],"ITEM_NONE"),new("pret:SPECIES_NIDOQUEEN",31,"NIDOQUEEN",44,0,[200, 195, 190, 201, 203, 207, 191, 191, 200],["pret:MOVE_BODY_SLAM", "pret:MOVE_DOUBLE_KICK", "pret:MOVE_POISON_STING", "pret:MOVE_EARTHQUAKE"],"ITEM_NONE"),new("pret:SPECIES_NIDOKING",34,"NIDOKING",45,0,[200, 195, 190, 201, 197, 195, 200, 193],["pret:MOVE_THRASH", "pret:MOVE_DOUBLE_KICK", "pret:MOVE_POISON_STING", "pret:MOVE_EARTHQUAKE"],"ITEM_NONE"),new("pret:SPECIES_RHYHORN",111,"RHYHORN",50,0,[204, 194, 211, 194, 201, 204, 200],["pret:MOVE_TAKE_DOWN", "pret:MOVE_ROCK_BLAST", "pret:MOVE_SCARY_FACE", "pret:MOVE_EARTHQUAKE"],"ITEM_NONE")],5000),
    }.ToImmutableDictionary();
    private static readonly ImmutableHashSet<string> ExpectedBattleKeys = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "ViridianCity_Gym_EventScript_Giovanni",
        "ViridianCity_Gym_EventScript_Jason",
        "ViridianCity_Gym_EventScript_Cole",
        "ViridianCity_Gym_EventScript_Atsushi",
        "ViridianCity_Gym_EventScript_Kiyo",
        "ViridianCity_Gym_EventScript_Takashi",
        "ViridianCity_Gym_EventScript_Samuel",
        "ViridianCity_Gym_EventScript_Yuji",
        "ViridianCity_Gym_EventScript_Warren");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = Expected.Keys.OrderBy(t => t.Value, StringComparer.Ordinal).ToImmutableArray();
    public static ImmutableArray<TrainerId> SupportedTrainers => AllTrainers;
    /// <summary>The eight regular trainers marked fought by set_gym_trainers 8 after Giovanni's win.</summary>
    public static ImmutableArray<TrainerId> GymTrainers { get; } = AllTrainers.Where(t => t != Giovanni).ToImmutableArray();
    public static int BasePrize(ViridianGymTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!Expected.TryGetValue(id, out var expected)) throw new NotSupportedException("viridian_gym_trainer_id");
        return expected.BasePrize;
    }
    public static TrainerPartyCreationResult Create(ViridianGymTrainerCatalog trainers, GameOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("viridian_gym_trainer_catalog");
        var source = opponents.Mechanics;
        var expected = Expected[id];
        bool customMoves = (trainer.PartyFlags & 1) != 0;
        for (int i = 0; i < trainer.Party.Length; i++)
        {
            var member = trainer.Party[i];
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("viridian_gym_trainer_species");
            var moves = expected.Party[i].Moves.Select(m => new MoveId(m));
            if (!customMoves && !PokemonMoveRules.InitialMovesCore(source.Catalog, source.Catalog.Species[member.Species], member.Level)
                .Select(m => m.Move).SequenceEqual(moves)) throw new NotSupportedException("viridian_gym_trainer_natural_moves");
            if (moves.Any(m => !source.Catalog.Moves.TryGetValue(m, out var move) || !SourceMoveDefinitionRules.IsValid(move)))
                throw new NotSupportedException("viridian_gym_trainer_move_source");
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        // PartyFlags=2 has a held item but retains the NPC factory's level-up moves.
        if (!customMoves) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(ViridianGymTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.BattlesByScript is null || catalog.Trainers.Count != 9 || catalog.BattlesByScript.Count != 9)
            throw new NotSupportedException("viridian_gym_trainer_scope");
        foreach (var (id, expected) in Expected)
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Party.IsDefault ||
                trainer.Party.Any(p => p is null || p.CustomMoves.IsDefault || p.EncodedSpeciesName.IsDefault) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault)
                throw new NotSupportedException("viridian_gym_trainer_source");
        foreach (var (script, battle) in catalog.BattlesByScript)
        {
            if (battle is null || script != battle.ScriptEntry) throw new NotSupportedException("viridian_gym_trainer_battle_key");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !ExpectedBattleKeys.Contains(battle.ScriptEntry))
            throw new NotSupportedException("viridian_gym_trainer_battle_source");
    }
}
