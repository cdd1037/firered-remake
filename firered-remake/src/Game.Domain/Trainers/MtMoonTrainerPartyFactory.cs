using Game.Domain.WildBattle;
using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

public sealed record MtMoonTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, ForestTrainerBattleDefinition> Battles);

/// <summary>The twelve source Mt. Moon trainers and their twenty-eight natural-move, no-item members.</summary>
public static class MtMoonTrainerPartyFactory
{
    public static TrainerId Miguel { get; } = new("pret:TRAINER_SUPER_NERD_MIGUEL");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = new[] {
        "YOUNGSTER_JOSH", "BUG_CATCHER_KENT", "BUG_CATCHER_ROBBY", "LASS_MIRIAM", "LASS_IRIS",
        "SUPER_NERD_JOVAN", "SUPER_NERD_MIGUEL", "HIKER_MARCOS", "TEAM_ROCKET_GRUNT",
        "TEAM_ROCKET_GRUNT_2", "TEAM_ROCKET_GRUNT_3", "TEAM_ROCKET_GRUNT_4"
    }.Select(x => new TrainerId("pret:TRAINER_" + x)).ToImmutableArray();

    public static TrainerPartyCreationResult Create(MtMoonTrainerCatalog trainers, PokemonCatalog pokemon,
        TrainerId trainerId, RngState rng)
    {

        ValidateCatalog(trainers);
        PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(trainerId, out var trainer)) throw new NotSupportedException("Unknown MtMoon trainer.");
        foreach (var member in trainer.Party)
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !MtMoonTrainerAi.ValidSpecies(species))
                throw new NotSupportedException("MtMoon party metadata differs from source.");
        return TrainerPartyFactory.CreateValidated(trainer, pokemon, rng);
    }

    public static void ValidateCatalog(MtMoonTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }

    public static void ValidateBattle(ForestTrainerBattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        var expected = BattleIdentity(battle.Trainer);
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


    private static (int Id, string Name) BattleIdentity(TrainerId id) => id.Value switch
    {
        "pret:TRAINER_YOUNGSTER_JOSH" => (91, "JOSH"),
        "pret:TRAINER_BUG_CATCHER_KENT" => (108, "KENT"),
        "pret:TRAINER_BUG_CATCHER_ROBBY" => (109, "ROBBY"),
        "pret:TRAINER_LASS_MIRIAM" => (120, "MIRIAM"),
        "pret:TRAINER_LASS_IRIS" => (121, "IRIS"),
        "pret:TRAINER_SUPER_NERD_JOVAN" => (169, "JOVAN"),
        "pret:TRAINER_SUPER_NERD_MIGUEL" => (170, "MIGUEL"),
        "pret:TRAINER_HIKER_MARCOS" => (181, "MARCOS"),
        "pret:TRAINER_TEAM_ROCKET_GRUNT" => (351, "GRUNT"),
        "pret:TRAINER_TEAM_ROCKET_GRUNT_2" => (352, "GRUNT"),
        "pret:TRAINER_TEAM_ROCKET_GRUNT_3" => (353, "GRUNT"),
        "pret:TRAINER_TEAM_ROCKET_GRUNT_4" => (354, "GRUNT"),
        _ => throw new NotSupportedException("Unknown trainer battle binding.")
    };
}
