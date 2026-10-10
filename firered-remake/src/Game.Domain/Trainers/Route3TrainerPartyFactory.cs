using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.Trainers;

public sealed record Route3TrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, ForestTrainerBattleDefinition> Battles);

/// <summary>The eight first-battle Route3 source parties, with natural moves and no items.</summary>
public static class Route3TrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-route3-trainers-v1";
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = new[] {
        "YOUNGSTER_BEN", "YOUNGSTER_CALVIN", "BUG_CATCHER_COLTON", "BUG_CATCHER_GREG", "BUG_CATCHER_JAMES",
        "LASS_JANICE", "LASS_SALLY", "LASS_ROBIN" }.Select(x => new TrainerId("pret:TRAINER_" + x)).ToImmutableArray();
    public static TrainerPartyCreationResult Create(Route3TrainerCatalog trainers, PokemonCatalog pokemon, TrainerId trainerId, RngState rng, bool mtMoonRules = false, bool ceruleanRules = false, bool billRules = false, bool vermilionRules = false, bool ssAnneRules = false, bool vermilionGymRules = false, bool route11Rules = false, bool rockTunnelRules = false)
    {
        route11Rules |= rockTunnelRules;
        vermilionGymRules |= route11Rules; ssAnneRules |= vermilionGymRules;
        ValidateCatalog(trainers); PokemonFactory.ValidateCatalog(pokemon);
        if (pokemon.Species.Count != (rockTunnelRules ? 93 : route11Rules ? 79 : ssAnneRules ? 73 : vermilionRules ? 70 : billRules ? 68 : ceruleanRules ? 65 : mtMoonRules ? 56 : 40)) throw new NotSupportedException("The explicit Route3 or MtMoon Pokemon catalog is required.");
        if (!trainers.Trainers.TryGetValue(trainerId, out var trainer)) throw new NotSupportedException("Unknown Route3 trainer.");
        foreach (var member in trainer.Party)
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !Route3TrainerAi.ValidSpecies(species))
                throw new NotSupportedException("Route3 party metadata differs from source.");
        return TrainerPartyFactory.CreateValidated(trainer, pokemon, rng);
    }
    public static void ValidateCatalog(Route3TrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster)
            throw new NotSupportedException("Unknown Route3 source/profile or complete-roster claim.");
        if (catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count != 8 || catalog.Battles.Count != 8)
            throw new ArgumentException("All eight Route3 trainers are required.");
        foreach (var id in SupportedTrainers)
        {
            var expected = Expected(id); bool female = id.Value.Contains("LASS_", StringComparison.Ordinal);
            string cls = female ? "LASS" : id.Value.Contains("BUG_CATCHER_", StringComparison.Ordinal) ? "BUG_CATCHER" : "YOUNGSTER";
            int clsId = cls == "YOUNGSTER" ? 57 : cls == "BUG_CATCHER" ? 58 : 59;
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Id != id ||
                trainer.SourceTrainerId != expected.Id || trainer.SourceTrainerName != expected.Name ||
                trainer.EncodedTrainerName.IsDefault || !trainer.EncodedTrainerName.SequenceEqual(Encode(expected.Name)) ||
                trainer.TrainerClass != "TRAINER_CLASS_" + cls || trainer.SourceTrainerClass != clsId ||
                trainer.TrainerPic != "TRAINER_PIC_" + cls || trainer.SourceTrainerPic != clsId + 25 ||
                trainer.EncounterMusicGender != (female ? 129 : 0) || trainer.DoubleBattle || trainer.PartyFlags != 0 ||
                trainer.AiFlags != 1 || trainer.BaseMoney != (cls == "BUG_CATCHER" ? 3 : 4) || trainer.Items.IsDefault ||
                !trainer.Items.SequenceEqual(new[] { "ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE" }) ||
                trainer.AiFlagSymbols.IsDefault || !trainer.AiFlagSymbols.SequenceEqual(new[] { "AI_SCRIPT_CHECK_BAD_MOVE" }))
                throw new NotSupportedException("Route3 trainer parameters differ from source.");
            if (trainer.Party.IsDefault || trainer.Party.Length != expected.Party.Length) throw new ArgumentException("Route3 party length differs.");
            for (int i = 0; i < expected.Party.Length; i++)
            {
                var member = trainer.Party[i]; var (species, level) = expected.Party[i]; string symbol = SpeciesSymbol(species);
                string display = species == 29 ? "NIDORAN♀" : symbol;
                if (member is null || member.SourceSpeciesId != species || member.Level != level || member.SourceIv != 0 ||
                    member.Species.Value != "pret:SPECIES_" + symbol || member.SpeciesName != display ||
                    member.EncodedSpeciesName.IsDefault || !member.EncodedSpeciesName.SequenceEqual(Encode(display)) ||
                    member.HeldItem != "ITEM_NONE" || member.CustomMoves.IsDefault || !member.CustomMoves.IsEmpty)
                    throw new NotSupportedException("Route3 party differs from its source default-move roster.");
            }
            if (!catalog.Battles.TryGetValue(id, out var battle) || battle is null || battle.Trainer != id) throw new ArgumentException("Missing Route3 battle.");
            ValidateBattle(battle);
        }
    }
    public static void ValidateBattle(ForestTrainerBattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        var expected = Expected(battle.Trainer); string name = expected.Name[..1] + expected.Name[1..].ToLowerInvariant();
        if (battle.Mode != 0 || battle.BattleTypeFlags != 8 || battle.ScriptEntry != "Route3_EventScript_" + name ||
            battle.IntroText != "Route3_Text_" + name + "Intro" || battle.DefeatText != "Route3_Text_" + name + "Defeat" ||
            battle.PostBattleText != "Route3_Text_" + name + "PostBattle" || battle.EntryScript != "EventScript_TryDoNormalTrainerBattle" ||
            battle.Callback != "CB2_EndTrainerBattle") throw new NotSupportedException("Route3 battle declaration differs from source.");
    }
    internal static int[] Encode(string text) => text.Select(c => c == '♀' ? 0xb6 : c == '♂' ? 0xb5 : 0xbb + c - 'A').ToArray();
    internal static string SpeciesSymbol(int species) => species switch { 10 => "CATERPIE", 11 => "METAPOD", 13 => "WEEDLE", 14 => "KAKUNA",
        16 => "PIDGEY", 19 => "RATTATA", 21 => "SPEAROW", 23 => "EKANS", 29 => "NIDORAN_F", 39 => "JIGGLYPUFF", _ => throw new NotSupportedException("Unknown Route3 trainer species.") };
    internal static (int Id, string Name, (int Species, int Level)[] Party) Expected(TrainerId id) => id.Value switch
    {
        "pret:TRAINER_YOUNGSTER_BEN" => (89, "BEN", [(19,11),(23,11)]),
        "pret:TRAINER_YOUNGSTER_CALVIN" => (90, "CALVIN", [(21,14)]),
        "pret:TRAINER_BUG_CATCHER_COLTON" => (105, "COLTON", [(10,10),(13,10),(10,10)]),
        "pret:TRAINER_BUG_CATCHER_GREG" => (106, "GREG", [(13,9),(14,9),(10,9),(11,9)]),
        "pret:TRAINER_BUG_CATCHER_JAMES" => (107, "JAMES", [(10,11),(11,11)]),
        "pret:TRAINER_LASS_JANICE" => (116, "JANICE", [(16,9),(16,9)]),
        "pret:TRAINER_LASS_SALLY" => (117, "SALLY", [(19,10),(29,10)]),
        "pret:TRAINER_LASS_ROBIN" => (118, "ROBIN", [(39,14)]),
        _ => throw new NotSupportedException("Unknown Route3 trainer.")
    };
}
