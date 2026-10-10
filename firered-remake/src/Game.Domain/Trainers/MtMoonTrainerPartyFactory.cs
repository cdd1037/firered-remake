using Game.Domain.WildBattle;
using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

public sealed record MtMoonTrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, ForestTrainerBattleDefinition> Battles);

/// <summary>The twelve source Mt. Moon trainers and their twenty-eight natural-move, no-item members.</summary>
public static class MtMoonTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-mtmoon-trainers-v1";
    public static TrainerId Miguel { get; } = new("pret:TRAINER_SUPER_NERD_MIGUEL");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = new[] {
        "YOUNGSTER_JOSH", "BUG_CATCHER_KENT", "BUG_CATCHER_ROBBY", "LASS_MIRIAM", "LASS_IRIS",
        "SUPER_NERD_JOVAN", "SUPER_NERD_MIGUEL", "HIKER_MARCOS", "TEAM_ROCKET_GRUNT",
        "TEAM_ROCKET_GRUNT_2", "TEAM_ROCKET_GRUNT_3", "TEAM_ROCKET_GRUNT_4"
    }.Select(x => new TrainerId("pret:TRAINER_" + x)).ToImmutableArray();

    public static TrainerPartyCreationResult Create(MtMoonTrainerCatalog trainers, PokemonCatalog pokemon,
        TrainerId trainerId, RngState rng, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false, bool celadonGymRules = false, bool rocketHideoutRules = false, bool towerRules = false, bool fuchsiaRules = false)
    {
        towerRules |= fuchsiaRules;
        rocketHideoutRules |= towerRules;
        celadonGymRules |= rocketHideoutRules;
        if ((Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) == 98) != (celadonGymRules && !rocketHideoutRules) || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) == 99) != (rocketHideoutRules && !towerRules) || (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) is 101 or 102) != (towerRules && !fuchsiaRules) || (pokemon.Species.Count == 107) != fuchsiaRules) throw new NotSupportedException("Explicit Celadon Gym trainer factory profile required.");
        rockTunnelRules |= celadonGymRules;
        route11Rules |= rockTunnelRules;
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules;
        ValidateCatalog(trainers);
        PokemonFactory.ValidateCatalog(pokemon);
        if (Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(pokemon) != (rockTunnelRules ? CeladonApproachBattleScope.SharedCatalogCount(pokemon) : route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : ceruleanRules ? 65 : 56)) throw new NotSupportedException("MtMoon requires its complete fifty-six-species catalog.");
        if (!trainers.Trainers.TryGetValue(trainerId, out var trainer)) throw new NotSupportedException("Unknown MtMoon trainer.");
        foreach (var member in trainer.Party)
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !MtMoonTrainerAi.ValidSpecies(species))
                throw new NotSupportedException("MtMoon party metadata differs from source.");
        return TrainerPartyFactory.CreateValidated(trainer, pokemon, rng);
    }

    public static void ValidateCatalog(MtMoonTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster)
            throw new NotSupportedException("Unknown MtMoon trainer source/profile or complete-roster claim.");
        if (catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count != 12 || catalog.Battles.Count != 12)
            throw new ArgumentException("All twelve MtMoon trainers are required.");
        foreach (var id in SupportedTrainers)
        {
            var expected = Expected(id);
            var identity = Identity(expected.Id);
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Id != id ||
                trainer.SourceTrainerId != expected.Id || trainer.SourceTrainerName != expected.Name ||
                trainer.EncodedTrainerName.IsDefault || !trainer.EncodedTrainerName.SequenceEqual(Encode(expected.Name)) ||
                trainer.TrainerClass != "TRAINER_CLASS_" + identity.Class || trainer.SourceTrainerClass != identity.ClassId ||
                trainer.TrainerPic != "TRAINER_PIC_" + identity.Pic || trainer.SourceTrainerPic != identity.PicId ||
                trainer.EncounterMusicGender != identity.EncounterMusicGender || trainer.DoubleBattle || trainer.PartyFlags != 0 ||
                trainer.AiFlags != 1 || trainer.BaseMoney != identity.Money || trainer.Items.IsDefault ||
                !trainer.Items.SequenceEqual(new[] { "ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE" }) ||
                trainer.AiFlagSymbols.IsDefault || !trainer.AiFlagSymbols.SequenceEqual(new[] { "AI_SCRIPT_CHECK_BAD_MOVE" }))
                throw new NotSupportedException("MtMoon trainer parameters differ from the pinned source.");
            if (trainer.Party.IsDefault || trainer.Party.Length != expected.Party.Length)
                throw new ArgumentException("MtMoon party length differs from source.");
            for (int i = 0; i < expected.Party.Length; i++)
            {
                var member = trainer.Party[i];
                var (species, level) = expected.Party[i];
                string symbol = SpeciesSymbol(species);
                if (member is null || member.SourceSpeciesId != species || member.Level != level || member.SourceIv != 0 ||
                    member.Species.Value != "pret:SPECIES_" + symbol || member.SpeciesName != symbol ||
                    member.EncodedSpeciesName.IsDefault || !member.EncodedSpeciesName.SequenceEqual(Encode(symbol)) ||
                    member.HeldItem != "ITEM_NONE" || member.CustomMoves.IsDefault || !member.CustomMoves.IsEmpty)
                    throw new NotSupportedException("MtMoon party differs from its source default-move roster.");
            }
            if (!catalog.Battles.TryGetValue(id, out var battle) || battle is null || battle.Trainer != id)
                throw new ArgumentException("Missing MtMoon battle declaration.");
            ValidateBattle(battle);
        }
    }

    public static void ValidateBattle(ForestTrainerBattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        var expected = Expected(battle.Trainer);
        bool miguel = battle.Trainer == Miguel;
        bool rocket = expected.Id is >= 351 and <= 354;
        string name = rocket ? "Grunt" + (expected.Id - 350) : expected.Name[..1] + expected.Name[1..].ToLowerInvariant();
        string map = miguel || rocket ? "MtMoon_B2F" : "MtMoon_1F";
        // Miguel's map script displays the intro itself, then invokes trainerbattle_no_intro (mode 3).
        // Its return address resumes the same script before the fossil-choice dialogue.
        if (battle.Mode != (miguel ? 3 : 0) || battle.BattleTypeFlags != 8 ||
            battle.ScriptEntry != map + "_EventScript_" + (miguel ? "BattleMiguel" : name) ||
            battle.IntroText != map + "_Text_" + name + "Intro" || battle.DefeatText != map + "_Text_" + name + "Defeat" ||
            battle.PostBattleText != (miguel ? "MtMoon_B2F_Text_WellEachTakeAFossil" : map + "_Text_" + name + "PostBattle") ||
            battle.EntryScript != (miguel ? "EventScript_DoNoIntroTrainerBattle" : "EventScript_TryDoNormalTrainerBattle") ||
            battle.Callback != "CB2_EndTrainerBattle")
            throw new NotSupportedException("MtMoon battle declaration differs from source.");
    }

    private static int[] Encode(string text) => text.Select(c => 0xbb + c - 'A').ToArray();

    private static (string Class, int ClassId, string Pic, int PicId, int EncounterMusicGender, int Money) Identity(int trainer) => trainer switch
    {
        91 => ("YOUNGSTER", 57, "YOUNGSTER", 82, 0, 4),
        108 or 109 => ("BUG_CATCHER", 58, "BUG_CATCHER", 83, 0, 3),
        120 or 121 => ("LASS", 59, "LASS", 84, 129, 4),
        169 or 170 => ("SUPER_NERD", 64, "SUPER_NERD", 89, 3, 6),
        181 => ("HIKER", 65, "HIKER", 90, 11, 9),
        351 or 352 or 353 or 354 => ("TEAM_ROCKET", 85, "ROCKET_GRUNT_M", 109, 6, 8),
        _ => throw new NotSupportedException("Unknown MtMoon trainer class.")
    };

    internal static string SpeciesSymbol(int species) => species switch
    {
        10 => "CATERPIE", 11 => "METAPOD", 13 => "WEEDLE", 14 => "KAKUNA", 19 => "RATTATA", 23 => "EKANS",
        27 => "SANDSHREW", 35 => "CLEFAIRY", 41 => "ZUBAT", 43 => "ODDISH", 69 => "BELLSPROUT", 74 => "GEODUDE",
        81 => "MAGNEMITE", 88 => "GRIMER", 95 => "ONIX", 100 => "VOLTORB", 109 => "KOFFING",
        _ => throw new NotSupportedException("Unknown MtMoon trainer species.")
    };

    internal static (int Id, string Name, (int Species, int Level)[] Party) Expected(TrainerId id) => id.Value switch
    {
        "pret:TRAINER_YOUNGSTER_JOSH" => (91, "JOSH", [(19, 10), (19, 10), (41, 10)]),
        "pret:TRAINER_BUG_CATCHER_KENT" => (108, "KENT", [(13, 11), (14, 11)]),
        "pret:TRAINER_BUG_CATCHER_ROBBY" => (109, "ROBBY", [(10, 10), (11, 10), (10, 10)]),
        "pret:TRAINER_LASS_MIRIAM" => (120, "MIRIAM", [(43, 11), (69, 11)]),
        "pret:TRAINER_LASS_IRIS" => (121, "IRIS", [(35, 14)]),
        "pret:TRAINER_SUPER_NERD_JOVAN" => (169, "JOVAN", [(81, 11), (100, 11)]),
        "pret:TRAINER_SUPER_NERD_MIGUEL" => (170, "MIGUEL", [(88, 12), (100, 12), (109, 12)]),
        "pret:TRAINER_HIKER_MARCOS" => (181, "MARCOS", [(74, 10), (74, 10), (95, 10)]),
        "pret:TRAINER_TEAM_ROCKET_GRUNT" => (351, "GRUNT", [(19, 13), (41, 13)]),
        "pret:TRAINER_TEAM_ROCKET_GRUNT_2" => (352, "GRUNT", [(27, 11), (19, 11), (41, 11)]),
        "pret:TRAINER_TEAM_ROCKET_GRUNT_3" => (353, "GRUNT", [(41, 11), (23, 11)]),
        "pret:TRAINER_TEAM_ROCKET_GRUNT_4" => (354, "GRUNT", [(19, 13), (27, 13)]),
        _ => throw new NotSupportedException("Unknown MtMoon trainer.")
    };
}
