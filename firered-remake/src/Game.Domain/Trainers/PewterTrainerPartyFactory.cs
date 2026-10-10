using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

public sealed record PewterTrainerBattleDefinition(TrainerId Trainer, int Mode, uint BattleTypeFlags,
    string ScriptEntry, string IntroText, string DefeatText, string PostBattleText,
    string? PostBattleScript, string? MusicOption, string EntryScript, string Callback, TrainerSourceRef Source);
public sealed record PewterTrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, PewterTrainerBattleDefinition> Battles);

/// <summary>The two original Pewter Gym custom-move, no-item parties. Existing lab/forest factories are unchanged.</summary>
public static class PewterTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-pewter-trainers-v1";
    public static TrainerId Liam { get; } = new("pret:TRAINER_CAMPER_LIAM");
    public static TrainerId Brock { get; } = new("pret:TRAINER_LEADER_BROCK");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = [Liam, Brock];

    public static TrainerPartyCreationResult Create(PewterTrainerCatalog trainers, PokemonCatalog pokemon,
        TrainerId trainerId, RngState rng)
    {
        ValidateCatalog(trainers);
        PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(trainerId, out var trainer))
            throw new NotSupportedException("Only Liam and Brock are supported by this trainer profile.");
        // Authenticate the executable six-move closure before any random OT draw.
        foreach (var member in trainer.Party)
            foreach (var id in member.CustomMoves.Where(m => m.Value != "pret:MOVE_NONE"))
                if (!pokemon.Moves.TryGetValue(id, out var move) || !PewterTrainerAi.ValidMove(move))
                    throw new NotSupportedException("Pewter custom move metadata differs from source.");
        var created = TrainerPartyFactory.CreateValidated(trainer, pokemon, rng);
        // SetMonMoveSlot after CreateMon changes no PID, IV, OT or RNG. The persisted representation
        // omits the source trailing MOVE_NONE slots, as the existing PokemonInstance contract requires.
        return created with { Party = created.Party.Select((member, i) => member with {
            Pokemon = member.Pokemon with { Moves = trainer.Party[i].CustomMoves
                .TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, pokemon.Moves[m].Pp, pokemon.Moves[m].Pp)).ToImmutableArray() }
        }).ToImmutableArray() };
    }

    public static void ValidateCatalog(PewterTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster)
            throw new NotSupportedException("Unknown Pewter source/profile or complete-roster claim.");
        if (catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count != 2 || catalog.Battles.Count != 2)
            throw new ArgumentException("Both Pewter Gym trainer definitions are required.");
        foreach (var id in SupportedTrainers)
        {
            bool brock = id == Brock;
            string name = brock ? "BROCK" : "LIAM";
            if (!catalog.Trainers.TryGetValue(id, out var t) || t is null || t.Id != id ||
                t.SourceTrainerId != (brock ? 414 : 142) || t.SourceTrainerName != name || !ValidEncoding(name, t.EncodedTrainerName) ||
                t.TrainerClass != (brock ? "TRAINER_CLASS_LEADER" : "TRAINER_CLASS_CAMPER") || t.SourceTrainerClass != (brock ? 84 : 61) ||
                t.TrainerPic != (brock ? "TRAINER_PIC_LEADER_BROCK" : "TRAINER_PIC_CAMPER") || t.SourceTrainerPic != (brock ? 116 : 86) ||
                t.EncounterMusicGender != 0 || t.DoubleBattle || t.PartyFlags != 1 || t.AiFlags != (brock ? 7u : 1u) ||
                t.BaseMoney != (brock ? 25 : 5) || t.Items.IsDefault || !t.Items.SequenceEqual(new[] { "ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE" }) ||
                t.AiFlagSymbols.IsDefault || !t.AiFlagSymbols.SequenceEqual(brock
                    ? new[] { "AI_SCRIPT_CHECK_BAD_MOVE", "AI_SCRIPT_TRY_TO_FAINT", "AI_SCRIPT_CHECK_VIABILITY" }
                    : new[] { "AI_SCRIPT_CHECK_BAD_MOVE" }))
                throw new NotSupportedException("Pewter trainer parameters differ from the pinned source.");
            if (t.Party.IsDefault || t.Party.Length != 2) throw new ArgumentException("Pewter trainers require two members.");
            for (int i = 0; i < 2; i++)
            {
                string species = i == 0 ? "GEODUDE" : brock ? "ONIX" : "SANDSHREW";
                int speciesId = i == 0 ? 74 : brock ? 95 : 27;
                int level = i == 0 ? (brock ? 12 : 10) : brock ? 14 : 11;
                string[] moves = i == 0 ? ["TACKLE", "DEFENSE_CURL", "NONE", "NONE"] : brock
                    ? ["TACKLE", "BIND", "ROCK_TOMB", "NONE"] : ["SCRATCH", "DEFENSE_CURL", "SAND_ATTACK", "NONE"];
                var m = t.Party[i];
                if (m is null || m.SourceSpeciesId != speciesId || m.Species.Value != "pret:SPECIES_" + species ||
                    m.SpeciesName != species || !ValidEncoding(species, m.EncodedSpeciesName) || m.Level != level ||
                    m.SourceIv != 0 || m.HeldItem != "ITEM_NONE" || m.CustomMoves.IsDefault ||
                    !m.CustomMoves.Select(x => x.Value).SequenceEqual(moves.Select(x => "pret:MOVE_" + x)))
                    throw new NotSupportedException("Pewter custom party differs from source.");
            }
            if (!catalog.Battles.TryGetValue(id, out var battle) || battle is null || battle.Trainer != id)
                throw new ArgumentException("Missing Pewter trainer battle definition.");
            ValidateBattle(battle);
        }
    }

    public static void ValidateBattle(PewterTrainerBattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        if (!SupportedTrainers.Contains(battle.Trainer)) throw new NotSupportedException("Unknown Pewter trainer.");
        bool brock = battle.Trainer == Brock;
        string name = brock ? "Brock" : "Liam";
        if (battle.Mode != (brock ? 1 : 0) || battle.BattleTypeFlags != 8 ||
            battle.ScriptEntry != "PewterCity_Gym_EventScript_" + name ||
            battle.IntroText != "PewterCity_Gym_Text_" + name + "Intro" ||
            battle.DefeatText != "PewterCity_Gym_Text_" + name + "Defeat" ||
            battle.PostBattleText != "PewterCity_Gym_Text_" + name + "PostBattle" ||
            battle.PostBattleScript != (brock ? "PewterCity_Gym_EventScript_DefeatedBrock" : null) ||
            battle.MusicOption != (brock ? "NO_MUSIC" : null) ||
            battle.EntryScript != "EventScript_TryDoNormalTrainerBattle" || battle.Callback != "CB2_EndTrainerBattle")
            throw new NotSupportedException("Unsupported Pewter battle script parameters.");
    }

    private static bool ValidEncoding(string name, ImmutableArray<int> bytes) =>
        !bytes.IsDefaultOrEmpty && bytes.Length == name.Length &&
        name.Select((c, i) => c is >= 'A' and <= 'Z' && bytes[i] == 0xbb + c - 'A').All(x => x);
}
