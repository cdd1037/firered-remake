using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record PretLabTrainerInput(string Path, int ByteLength, string GitBlobSha1, string Sha256);
public sealed record PretLabTrainerDocument(int SchemaVersion, string Profile, string SourceCommit,
    bool CompleteSourceRoster, ImmutableArray<PretLabTrainerInput> Inputs, ImmutableArray<string> Limitations,
    ImmutableArray<TrainerSourceRef> RulesSources, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<EarlyRivalBattleDefinition> Battles);

/// <summary>Strict UTF-8 artifact identity and source provenance; caller owns local filesystem access.</summary>
public static class PretLabTrainerCatalog
{
    public const string GeneratedSha256 = "726c4609d4d840df0bdd38547d6f931699b5c6a823e18c5601c6a3ccbdfc83be";

    public static TrainerCatalog Load(string generatedJson)
    {
        var doc = ContentJson.ParseStrict<PretLabTrainerDocument>(generatedJson, ContentJson.CreateOptions());
        if (doc.SchemaVersion != 1 || doc.Profile != TrainerPartyFactory.RulesVersion ||
            doc.SourceCommit != TrainerPartyFactory.SourceCommit || doc.CompleteSourceRoster)
            throw ContentJson.Error("trainer_profile", "$", "Unknown trainer source/profile or unsupported full-roster claim.");
        if (doc.Inputs.IsDefaultOrEmpty || doc.Limitations.IsDefaultOrEmpty || doc.RulesSources.IsDefaultOrEmpty ||
            doc.Trainers.IsDefaultOrEmpty || doc.Battles.IsDefaultOrEmpty)
            throw ContentJson.Error("trainer_missing_data", "$", "Explicit inputs, limitations, rules, trainers and battles are required.");
        var inputs = new Dictionary<string, PretLabTrainerInput>(StringComparer.Ordinal);
        foreach (var input in doc.Inputs)
            if (input is null || input.Path is null || !ExpectedInputs.TryGetValue(input.Path, out var expected) ||
                input != expected || !inputs.TryAdd(input.Path, input))
                throw ContentJson.Error("trainer_source_identity", "$.inputs", "Input differs from the independently pinned source manifest.");
        if (inputs.Count != ExpectedInputs.Count)
            throw ContentJson.Error("trainer_source_missing", "$.inputs", "The complete bounded source manifest is required.");

        void Source(TrainerSourceRef source, string? expectedPath = null)
        {
            if (source is null || source.Path is null || (expectedPath is not null && source.Path != expectedPath) ||
                string.IsNullOrWhiteSpace(source.Symbol) || source.StartLine < 1 || source.EndLine < source.StartLine ||
                !inputs.TryGetValue(source.Path, out var input) || source.Sha256 != input.Sha256)
                throw ContentJson.Error("trainer_source_ref", "$", "Invalid source line, symbol or hash.");
        }
        foreach (var rule in doc.RulesSources) Source(rule);
        foreach (var trainer in doc.Trainers)
        {
            if (trainer is null || trainer.Party.IsDefaultOrEmpty)
                throw ContentJson.Error("trainer_party", "$.trainers", "Null trainer or missing party.");
            Source(trainer.Source, "src/data/trainers.h");
            Source(trainer.MoneySource, "src/battle_main.c");
            if (trainer.Id.Value != "pret:" + trainer.Source.Symbol)
                throw ContentJson.Error("trainer_symbol", "$.trainers", "Trainer ID must preserve its source symbol.");
            foreach (var member in trainer.Party)
            {
                if (member is null) throw ContentJson.Error("trainer_member", "$.trainers", "Null party member.");
                Source(member.Source, "src/data/trainer_parties.h");
                Source(member.SpeciesNameSource, "src/data/text/species_names.h");
                if (member.Species.Value != "pret:" + member.SpeciesNameSource.Symbol)
                    throw ContentJson.Error("trainer_species_symbol", "$.trainers", "Species ID must preserve its source symbol.");
            }
        }
        foreach (var battle in doc.Battles)
        {
            if (battle is null) throw ContentJson.Error("trainer_battle", "$.battles", "Null battle parameters.");
            Source(battle.Source, "data/maps/PalletTown_ProfessorOaksLab/scripts.inc");
            if (battle.Trainer.Value != "pret:" + battle.Source.Symbol)
                throw ContentJson.Error("trainer_battle_symbol", "$.battles", "Battle must preserve its source trainer symbol.");
        }
        try
        {
            var catalog = new TrainerCatalog(doc.SourceCommit, doc.Profile, doc.CompleteSourceRoster,
                doc.Trainers.ToImmutableDictionary(t => t.Id), doc.Battles.ToImmutableDictionary(b => b.Trainer));
            TrainerPartyFactory.ValidateCatalog(catalog);
            if (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(generatedJson))) != GeneratedSha256)
                throw ContentJson.Error("trainer_generated_hash", "$", "Generated bytes differ from the reviewed deterministic trainer catalog.");
            return catalog;
        }
        catch (ArgumentException ex) { throw ContentJson.Error("trainer_catalog", "$", ex.Message); }
        catch (NotSupportedException ex) { throw ContentJson.Error("trainer_catalog", "$", ex.Message); }
    }

    // Byte identities only. The source data itself remains local and gitignored.
    private static readonly IReadOnlyDictionary<string, PretLabTrainerInput> ExpectedInputs =
        new Dictionary<string, PretLabTrainerInput>(StringComparer.Ordinal)
        {
            ["asm/macros/event.inc"] = new("asm/macros/event.inc", 56947, "142ad979d7d3093244087a247fa2b4883424f77f", "848e5e791adeddbe7ae7fe2717729af666a8d8aa163ab7781c74bfd49ce58671"),
            ["charmap.txt"] = new("charmap.txt", 21853, "b9d0ed9de00d05fc303bb987a5aa634b19009b47", "4da662317b3b5109a52064f9012d85b644d1dbf0f3f24243374aeef4cf25f061"),
            ["data/battle_ai_scripts.s"] = new("data/battle_ai_scripts.s", 85293, "85d043d41773d2a6d33766a5f1c03e70668dac94", "0971513157b28ba27ce272acaa2388afa7362a302711f737cad6c3598ca1b389"),
            ["data/maps/PalletTown_ProfessorOaksLab/scripts.inc"] = new("data/maps/PalletTown_ProfessorOaksLab/scripts.inc", 47604, "90adf867df540efc42b5ef98307a882b8aa97987", "ed6e02fdd179af05159f27288ef9b5226ce7917c03ae54e8fb8d83c853fc311d"),
            ["data/scripts/trainer_battle.inc"] = new("data/scripts/trainer_battle.inc", 3768, "c3e4371962dae55fedfd0b5c2f02068a1f8cf0e7", "409fdc28f13423f602cb2428208693a4c467a1f5785bc487bafb5ab0fce8c96c"),
            ["include/battle.h"] = new("include/battle.h", 21838, "3771ef33a1fec9aacad85d29ebc96c962cf59261", "bf64a16fb4e660f8a326eb352a511bb9e8fd54a74ebb03e328c08d5a23e5d8fa"),
            ["include/constants/battle.h"] = new("include/constants/battle.h", 16440, "d46776ab9b2276e7591d3b964937d3ea69c574c1", "b9d0493af4629d450050637e9aace63b684b282f841bed2a0d97b1d71e9d55e3"),
            ["include/constants/battle_ai.h"] = new("include/constants/battle_ai.h", 1436, "d23648f390a4d6bb28cb3970af9d2af24b6a46b6", "b9493a13b76892fea0c5beb43ae4c5da3a02ef0575bbffd02e17fa93f25f5ce5"),
            ["include/constants/battle_setup.h"] = new("include/constants/battle_setup.h", 701, "1e804fd1868e00406528459a031ec6dd82616109", "0b6b1ca9cc25f88a467790ad1f5af3ea0dcef51852e4c025df63b688414762d1"),
            ["include/constants/items.h"] = new("include/constants/items.h", 12436, "cfc42256c33bb6b807630deacadad8071a3eeee3", "af17276346eaecc5a10ac60f14b22c33332ec8c6fb0ec82895bdc3649b678138"),
            ["include/constants/moves.h"] = new("include/constants/moves.h", 10249, "14738f1b21597269ba1934762e437dec3b4cc643", "79d5b93423006c25e6ee47355b15f403b2534616fd6b638e1755e5886d378194"),
            ["include/constants/opponents.h"] = new("include/constants/opponents.h", 39946, "3b893f217b2b12f29540f0b4db2889e66e98d74a", "01ae04c7fd3837c818f1b9ef082313cfeee4c9d3907441b4e192865a626247ee"),
            ["include/constants/pokemon.h"] = new("include/constants/pokemon.h", 10894, "21c678744f21693825a0c03bb73efa77a256af8b", "9cabd203100ae7246bc595b7295e7e04145f6292abfab83c3bd0597a2cfbcfb5"),
            ["include/constants/species.h"] = new("include/constants/species.h", 12982, "7446cc1380995ab62deff8359bad813aa2b936e9", "ec3f9f9c2717dcd453aa7a27506c32e1e6ca067864534dee494695d74344c51a"),
            ["include/constants/trainers.h"] = new("include/constants/trainers.h", 20887, "70e8b4bc79e9a38c41d6526921053dd4e7ef7c2e", "5f6e6585ba511fcf11f970f60132f45db35caf6cec9f6ae56b6bc57b979366c9"),
            ["include/pokemon.h"] = new("include/pokemon.h", 16352, "414dc41767295aed38f299e8495c5e2f8d25ddc9", "a6881fee07ae10f57ebf3ec7af1cabd2e900d8ba8d3da644313c087d16e1617e"),
            ["include/random.h"] = new("include/random.h", 669, "fd6b88ad0af62f22951e6e81fa21e38b12480ece", "8a6049183598b2d865b5ff522abdb3d0e94012a127d724af46b9b7b69803f5b9"),
            ["src/battle_ai_script_commands.c"] = new("src/battle_ai_script_commands.c", 55810, "4df9977bc53d57ce380644ccd1cb6c3f7e7767c9", "0b297aa963de7a7810d3b635a95e9ab68a2594aaf4bec77d946a0e068d3a47c4"),
            ["src/battle_controller_opponent.c"] = new("src/battle_controller_opponent.c", 73524, "b4b6ebbdf019fbfb2c0065f7bc42166fc5720fd0", "1b4393635fc4e199203829ad078bda665c9b9c82a487fe17fbd6cac12a5dba98"),
            ["src/battle_main.c"] = new("src/battle_main.c", 171904, "64c9e59b7efdd2acf399716da40d4848da91a428", "c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928"),
            ["src/battle_setup.c"] = new("src/battle_setup.c", 33351, "6d62084dbb697fc3a42d49a6dc865657339dcea2", "f33e6cdd3ff22ab9fe0f43219dfc795a9abfcc5f01c04e96809b98ddf5b952fd"),
            ["src/data/battle_moves.h"] = new("src/data/battle_moves.h", 121457, "c6457675d5441010feebbb5aec58dc9e37356134", "54e08c749547afaf73eb2f1e4f4e3023f4d8e186d2f608c29bd6419c291b5384"),
            ["src/data/pokemon/level_up_learnsets.h"] = new("src/data/pokemon/level_up_learnsets.h", 182027, "bdf1f9fbb9ad2791fcaec31826dc30d180fabbe0", "d6563a88fa893d1433a02027cb13ba9574a6828a96b1a730b1d767fd7c5a0cc6"),
            ["src/data/pokemon/species_info.h"] = new("src/data/pokemon/species_info.h", 342600, "3e36c08ced6dec3d427673d8b2f787221a178aa2", "9c2da1284f7859700b315abda0499334c422f2c6776d576c788fcd1e4050f71e"),
            ["src/data/text/species_names.h"] = new("src/data/text/species_names.h", 15851, "25f2c9aa2363202e75cf2080be01465b2d64b5c4", "73e87117c98828423b7fc15565bd2a839a0671665fb7c1c1b26264d7d7a8912f"),
            ["src/data/trainer_parties.h"] = new("src/data/trainer_parties.h", 242256, "2e5780022c305165d76d613eaeb682155c8b0de6", "cb135d9cbdfc52c87066374d582506830a8c93561eab2033d847331c2de00299"),
            ["src/data/trainers.h"] = new("src/data/trainers.h", 302118, "02afbfd16d5cef254362577004d0104282eb2538", "b2105a3b1d1baec02d8f6a89f44bcdf6a05cbaac1d6d8872766eec49aa8555bc"),
            ["src/pokemon.c"] = new("src/pokemon.c", 221865, "66a5c3d233e8b1b85fe5045aeb6667332cd61d4c", "116a470cd9ebfa226aa8d85c2eb63591e176d726837c67baaa1ac6260d50f2ec"),
            ["src/random.c"] = new("src/random.c", 323, "635a1df5a59fa42a88f30ad884405db2bde95ea5", "e7e90cc070d1fbf09579f5f91762cd8875d5bbe1d7a90f468bcd870df73b9de2"),
        };
}
