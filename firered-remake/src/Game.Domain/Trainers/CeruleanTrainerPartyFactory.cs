using Game.Domain.WildBattle;
using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>Seven pinned Cerulean parties: three ordinary trainers, Misty, and three starter branches.</summary>
public static class CeruleanTrainerPartyFactory
{
    public static TrainerId Misty { get; } = new("pret:TRAINER_LEADER_MISTY");
    public static TrainerId Diana { get; } = new("pret:TRAINER_PICNICKER_DIANA");
    public static TrainerId Luis { get; } = new("pret:TRAINER_SWIMMER_MALE_LUIS");
    public static TrainerId Rocket { get; } = new("pret:TRAINER_TEAM_ROCKET_GRUNT_5");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = new[] { "LEADER_MISTY", "PICNICKER_DIANA",
        "SWIMMER_MALE_LUIS", "RIVAL_CERULEAN_SQUIRTLE", "RIVAL_CERULEAN_BULBASAUR", "RIVAL_CERULEAN_CHARMANDER",
        "TEAM_ROCKET_GRUNT_5" }.Select(x => new TrainerId("pret:TRAINER_" + x)).ToImmutableArray();
    public static TrainerId ForPlayerStarter(SpeciesId starter) => starter.Value switch {
        "pret:SPECIES_BULBASAUR" => SupportedTrainers[3], "pret:SPECIES_CHARMANDER" => SupportedTrainers[4],
        "pret:SPECIES_SQUIRTLE" => SupportedTrainers[5], _ => throw new NotSupportedException("Unknown original starter.") };

    public static TrainerPartyCreationResult Create(CeruleanTrainerCatalog trainers, PokemonCatalog pokemon,
        TrainerId trainerId, RngState rng)
    {

        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(trainerId, out var trainer))
            throw new NotSupportedException("Cerulean requires its complete sixty-five-species catalog and a source trainer.");
        foreach (var member in trainer.Party)
        {
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !CeruleanTrainerAi.ValidSpecies(species))
                throw new NotSupportedException("Cerulean trainer species differs from source.");

        }
        var created = TrainerPartyFactory.CreateValidated(trainer, pokemon, rng);
        if (trainer.PartyFlags == 0) return created;
        return created with { Party = created.Party.Select((member, i) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, pokemon.Moves[m].Pp, pokemon.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }

    public static void ValidateCatalog(CeruleanTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition b)
    {
        ArgumentNullException.ThrowIfNull(b); var e = BattleIdentity(b.Trainer);
        bool rival = e.Id is >= 332 and <= 334, grunt = b.Trainer == Rocket, misty = b.Trainer == Misty;
        string map = rival || grunt ? "CeruleanCity" : "CeruleanCity_Gym";
        string name = rival ? "Rival" : grunt ? "Grunt" : e.Name[..1] + e.Name[1..].ToLowerInvariant();
        string suffix = rival ? e.Id switch { 332 => "Squirtle", 333 => "Bulbasaur", _ => "Charmander" } : "";
        if (b.Mode != (rival || grunt ? 3 : misty ? 1 : 0) || b.RivalFlags != 0 || b.BattleTypeFlags != 8 ||
            b.ScriptEntry != map + "_EventScript_" + name + suffix || b.IntroText != map + "_Text_" + name + "Intro" ||
            b.DefeatText != map + "_Text_" + name + "Defeat" || b.VictoryText != "" ||
            b.PostBattleText != (misty ? "CeruleanCity_Gym_Text_ExplainCascadeBadge" : grunt ? "CeruleanCity_Text_RecoveredTM28FromGrunt" : map + "_Text_" + name + "PostBattle") ||
            b.EntryScript != (rival || grunt ? "EventScript_DoNoIntroTrainerBattle" : "EventScript_TryDoNormalTrainerBattle") ||
            b.Callback != "CB2_EndTrainerBattle" || b.PostBattleScript != (misty ? "CeruleanCity_Gym_EventScript_MistyDefeated" : null) ||
            b.MusicOption != (misty ? "NO_MUSIC" : null)) throw new NotSupportedException("Cerulean battle declaration differs from source: " + b);
    }

    private static (int Id, string Name) BattleIdentity(TrainerId id) => id.Value switch
    {
        "pret:TRAINER_LEADER_MISTY" => (415, "MISTY"),
        "pret:TRAINER_PICNICKER_DIANA" => (150, "DIANA"),
        "pret:TRAINER_SWIMMER_MALE_LUIS" => (234, "LUIS"),
        "pret:TRAINER_TEAM_ROCKET_GRUNT_5" => (355, "GRUNT"),
        "pret:TRAINER_RIVAL_CERULEAN_SQUIRTLE" => (332, "TERRY"),
        "pret:TRAINER_RIVAL_CERULEAN_BULBASAUR" => (333, "TERRY"),
        "pret:TRAINER_RIVAL_CERULEAN_CHARMANDER" => (334, "TERRY"),
        _ => throw new NotSupportedException("Unknown trainer battle binding.")
    };

}
