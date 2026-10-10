using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>Source early-rival script parameters. This is data; it does not execute victory/whiteout callbacks.</summary>
public sealed record Route22TrainerBattleDefinition(TrainerId Trainer, int PlayerStarterVar, SpeciesId PlayerStarter,
    int Mode, int RivalFlags, uint BattleTypeFlags, int WonTrainerFlag, int BasePrizeMoney,
    string ScriptEntry, string DefeatText, string VictoryText, string EntryScript, string Callback, TrainerSourceRef Source);
public sealed record Route22TrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, Route22TrainerBattleDefinition> Battles);

/// <summary>Only the three source Route22 early-rival custom-move parties; never a level-nine natural learnset.</summary>
public static class Route22TrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-route22-rival-trainers-v1";
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } =
        new[] { "SQUIRTLE", "BULBASAUR", "CHARMANDER" }
            .Select(x => new TrainerId("pret:TRAINER_RIVAL_ROUTE22_EARLY_" + x)).ToImmutableArray();

    public static TrainerId ForPlayerStarter(SpeciesId starter) => starter.Value switch
    {
        "pret:SPECIES_BULBASAUR" => SupportedTrainers[0],
        "pret:SPECIES_CHARMANDER" => SupportedTrainers[1],
        "pret:SPECIES_SQUIRTLE" => SupportedTrainers[2],
        _ => throw new NotSupportedException("Only the three original player starters select an early Route22 rival.")
    };

    public static TrainerPartyCreationResult Create(Route22TrainerCatalog trainers, PokemonCatalog pokemon,
        TrainerId trainerId, RngState rng, bool route3Rules = false, bool mtMoonRules = false, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false)
    {
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules;
        ValidateCatalog(trainers);
        PokemonFactory.ValidateCatalog(pokemon);
        if (pokemon.Species.Count != (route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : ceruleanRules ? 65 : mtMoonRules ? 56 : route3Rules ? 40 : 32)) throw new NotSupportedException("The versioned early-rival Pokemon catalog is required.");
        if (!trainers.Trainers.TryGetValue(trainerId, out var trainer))
            throw new NotSupportedException("Only the three Route22 early rivals are supported.");
        foreach (var member in trainer.Party)
        {
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !Route22TrainerAi.ValidSpecies(species))
                throw new NotSupportedException("Rival species metadata differs from the pinned source.");
            foreach (var id in member.CustomMoves.Where(m => m.Value != "pret:MOVE_NONE"))
                if (!pokemon.Moves.TryGetValue(id, out var move) || !Route22TrainerAi.ValidMove(move))
                    throw new NotSupportedException("Rival custom move metadata differs from the pinned source.");
        }
        var created = TrainerPartyFactory.CreateValidated(trainer, pokemon, rng);
        // Source SetMonData follows CreateMon. Compact persisted slots omit two trailing MOVE_NONE.
        return created with { Party = created.Party.Select((member, i) => member with {
            Pokemon = member.Pokemon with { Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, pokemon.Moves[m].Pp, pokemon.Moves[m].Pp)).ToImmutableArray() }
        }).ToImmutableArray() };
    }

    public static void ValidateCatalog(Route22TrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster)
            throw new NotSupportedException("Unknown Route22 rival source/profile or complete-roster claim.");
        if (catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count != 3 || catalog.Battles.Count != 3)
            throw new ArgumentException("All three bounded early-rival definitions are required.");
        for (int branch = 0; branch < 3; branch++)
        {
            var id = SupportedTrainers[branch];
            if (!catalog.Trainers.TryGetValue(id, out var t) || t is null || t.Id != id || t.SourceTrainerId != 329 + branch ||
                t.SourceTrainerName != "TERRY" || !ValidEncoding("TERRY", t.EncodedTrainerName) ||
                t.TrainerClass != "TRAINER_CLASS_RIVAL_EARLY" || t.SourceTrainerClass != 81 ||
                t.TrainerPic != "TRAINER_PIC_RIVAL_EARLY" || t.SourceTrainerPic != 106 ||
                t.EncounterMusicGender != 0 || t.DoubleBattle || t.PartyFlags != 1 || t.AiFlags != 7 || t.BaseMoney != 4 ||
                t.Items.IsDefault || !t.Items.SequenceEqual(new[] { "ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE" }) ||
                t.AiFlagSymbols.IsDefault || !t.AiFlagSymbols.SequenceEqual(new[] { "AI_SCRIPT_CHECK_BAD_MOVE", "AI_SCRIPT_TRY_TO_FAINT", "AI_SCRIPT_CHECK_VIABILITY" }))
                throw new NotSupportedException("Route22 rival parameters differ from source; TERRY is the canonical generation name.");
            if (t.Party.IsDefault || t.Party.Length != 2) throw new ArgumentException("The early rival has two Pokemon.");
            for (int index = 0; index < 2; index++)
            {
                string species = index == 0 ? "PIDGEY" : branch switch { 0 => "SQUIRTLE", 1 => "BULBASAUR", _ => "CHARMANDER" };
                int sourceId = index == 0 ? 16 : branch switch { 0 => 7, 1 => 1, _ => 4 };
                string[] moves = index == 0 ? ["TACKLE", "SAND_ATTACK", "NONE", "NONE"] : branch switch
                { 0 => ["TACKLE", "TAIL_WHIP", "NONE", "NONE"], 1 => ["TACKLE", "GROWL", "NONE", "NONE"], _ => ["SCRATCH", "GROWL", "NONE", "NONE"] };
                var m = t.Party[index];
                if (m is null || m.Species.Value != "pret:SPECIES_" + species || m.SpeciesName != species || m.SourceSpeciesId != sourceId ||
                    !ValidEncoding(species, m.EncodedSpeciesName) || m.Level != 9 || m.SourceIv != 50 || m.HeldItem != "ITEM_NONE" ||
                    m.CustomMoves.IsDefault || !m.CustomMoves.Select(x => x.Value).SequenceEqual(moves.Select(x => "pret:MOVE_" + x)))
                    throw new NotSupportedException("Route22 rival party differs from its source custom-move roster.");
            }
            if (!catalog.Battles.TryGetValue(id, out var battle) || battle is null || battle.Trainer != id)
                throw new ArgumentException("Missing early Route22 rival battle declaration.");
            ValidateBattle(battle);
        }
    }

    public static void ValidateBattle(Route22TrainerBattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        int branch = SupportedTrainers.IndexOf(battle.Trainer);
        if (branch < 0) throw new NotSupportedException("Unknown Route22 early rival.");
        string rival = branch switch { 0 => "Squirtle", 1 => "Bulbasaur", _ => "Charmander" };
        string starter = branch switch { 0 => "BULBASAUR", 1 => "CHARMANDER", _ => "SQUIRTLE" };
        if (battle.PlayerStarterVar != 2 - branch || battle.PlayerStarter.Value != "pret:SPECIES_" + starter ||
            battle.Mode != 9 || battle.RivalFlags != 0 || battle.BattleTypeFlags != 8 || battle.WonTrainerFlag != 1609 + branch ||
            battle.BasePrizeMoney != 144 || battle.ScriptEntry != "Route22_EventScript_EarlyRival" + rival ||
            battle.DefeatText != "Route22_Text_EarlyRivalDefeat" || battle.VictoryText != "Text_RivalVictory" ||
            battle.EntryScript != "EventScript_DoNoIntroTrainerBattle" || battle.Callback != "CB2_EndTrainerBattle")
            throw new NotSupportedException("Route22 battle declaration differs; this is not the lab tutorial/heal battle.");
    }

    private static bool ValidEncoding(string text, ImmutableArray<int> bytes) =>
        !bytes.IsDefaultOrEmpty && bytes.Length == text.Length &&
        text.Select((c, i) => c is >= 'A' and <= 'Z' && bytes[i] == 0xbb + c - 'A').All(x => x);
}
