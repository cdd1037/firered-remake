using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

public sealed record ForestTrainerBattleDefinition(TrainerId Trainer, int Mode, uint BattleTypeFlags,
    string ScriptEntry, string IntroText, string DefeatText, string PostBattleText,
    string EntryScript, string Callback, TrainerSourceRef Source);

public sealed record ForestTrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, ForestTrainerBattleDefinition> Battles);

/// <summary>The five original Viridian Forest Bug Catchers, no held items or custom moves.</summary>
public static class ForestTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-forest-trainers-v1";
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } =
        new[] { "RICK", "DOUG", "SAMMY", "ANTHONY", "CHARLIE" }
            .Select(x => new TrainerId("pret:TRAINER_BUG_CATCHER_" + x)).ToImmutableArray();

    public static TrainerPartyCreationResult Create(ForestTrainerCatalog trainers, PokemonCatalog pokemon,
        TrainerId trainerId, RngState rng)
    {
        ValidateCatalog(trainers);
        PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(trainerId, out var trainer))
            throw new NotSupportedException("Only the five Viridian Forest trainers are supported.");
        return TrainerPartyFactory.CreateValidated(trainer, pokemon, rng);
    }

    public static void ValidateCatalog(ForestTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster)
            throw new NotSupportedException("Unknown forest trainer source/profile or complete-roster claim.");
        if (catalog.Trainers is null || catalog.Battles is null || catalog.Trainers.Count != 5 || catalog.Battles.Count != 5)
            throw new ArgumentException("The complete bounded five-trainer catalog is required.");
        foreach (var id in SupportedTrainers)
        {
            var name = id.Value["pret:TRAINER_BUG_CATCHER_".Length..];
            var (sourceId, party) = Expected(name);
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Id != id ||
                trainer.SourceTrainerId != sourceId || trainer.SourceTrainerName != name ||
                !ValidEncoding(name, trainer.EncodedTrainerName) ||
                trainer.TrainerClass != "TRAINER_CLASS_BUG_CATCHER" || trainer.SourceTrainerClass != 58 ||
                trainer.TrainerPic != "TRAINER_PIC_BUG_CATCHER" || trainer.SourceTrainerPic != 83 ||
                trainer.EncounterMusicGender != 0 || trainer.DoubleBattle || trainer.PartyFlags != 0 ||
                trainer.AiFlags != 1 || trainer.BaseMoney != 3 || trainer.Items.IsDefault ||
                !trainer.Items.SequenceEqual(new[] { "ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE" }) ||
                trainer.AiFlagSymbols.IsDefault || !trainer.AiFlagSymbols.SequenceEqual(new[] { "AI_SCRIPT_CHECK_BAD_MOVE" }))
                throw new NotSupportedException("Unsupported or inconsistent forest trainer parameters.");
            if (trainer.Party.IsDefault || trainer.Party.Length != party.Length)
                throw new ArgumentException("Forest trainer party length differs from source.");
            for (int i = 0; i < party.Length; i++)
            {
                var member = trainer.Party[i];
                var expected = party[i];
                string species = expected.Species switch { 10 => "CATERPIE", 11 => "METAPOD", 13 => "WEEDLE", 14 => "KAKUNA", _ => throw new InvalidOperationException() };
                if (member is null || member.SourceSpeciesId != expected.Species || member.Level != expected.Level ||
                    member.Species.Value != "pret:SPECIES_" + species || member.SpeciesName != species ||
                    !ValidEncoding(species, member.EncodedSpeciesName) || member.SourceIv != 0 ||
                    member.HeldItem != "ITEM_NONE" || member.CustomMoves.IsDefault || !member.CustomMoves.IsEmpty)
                    throw new NotSupportedException("Forest trainer party differs from source no-item/default-move roster.");
            }
            if (!catalog.Battles.TryGetValue(id, out var battle) || battle is null || battle.Trainer != id)
                throw new ArgumentException("Missing forest trainer battle definition.");
            ValidateBattle(battle);
        }
    }

    public static void ValidateBattle(ForestTrainerBattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        if (!SupportedTrainers.Contains(battle.Trainer)) throw new NotSupportedException("Unknown forest trainer.");
        string upper = battle.Trainer.Value["pret:TRAINER_BUG_CATCHER_".Length..];
        string name = upper[..1] + upper[1..].ToLowerInvariant();
        if (battle.Mode != 0 || battle.BattleTypeFlags != 8 || battle.ScriptEntry != "ViridianForest_EventScript_" + name ||
            battle.IntroText != "ViridianForest_Text_" + name + "Intro" || battle.DefeatText != "ViridianForest_Text_" + name + "Defeat" ||
            battle.PostBattleText != "ViridianForest_Text_" + name + "PostBattle" ||
            battle.EntryScript != "EventScript_TryDoNormalTrainerBattle" || battle.Callback != "CB2_EndTrainerBattle")
            throw new NotSupportedException("Unsupported forest normal-single battle parameters.");
    }

    private static bool ValidEncoding(string name, ImmutableArray<int> bytes) =>
        !bytes.IsDefaultOrEmpty && bytes.Length == name.Length &&
        name.Select((c, i) => c is >= 'A' and <= 'Z' && bytes[i] == 0xbb + c - 'A').All(x => x);

    private static (int Id, (int Species, int Level)[] Party) Expected(string name) => name switch
    {
        "RICK" => (102, [(13, 6), (10, 6)]),
        "DOUG" => (103, [(13, 7), (14, 7), (13, 7)]),
        "SAMMY" => (104, [(13, 9)]),
        "ANTHONY" => (531, [(10, 7), (10, 8)]),
        "CHARLIE" => (532, [(11, 7), (10, 7), (11, 7)]),
        _ => throw new NotSupportedException("Unknown forest trainer.")
    };
}
