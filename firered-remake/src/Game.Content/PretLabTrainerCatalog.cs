using System.Collections.Immutable;
using System.Text;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record PretLabTrainerInput(string Path);
public sealed record PretLabTrainerDocument(int SchemaVersion, string Profile, string SourceCommit,
    bool CompleteSourceRoster, ImmutableArray<PretLabTrainerInput> Inputs, ImmutableArray<string> Limitations,
    ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<EarlyRivalBattleDefinition> Battles);

/// <summary>Strict UTF-8 artifact identity and source provenance; caller owns local filesystem access.</summary>
public static class PretLabTrainerCatalog
{

    public static TrainerCatalog Load(string generatedJson)
    {
        var doc = ContentJson.ParseStrict<PretLabTrainerDocument>(generatedJson, ContentJson.CreateOptions());
        if (doc.SchemaVersion != 1 || doc.CompleteSourceRoster)
            throw ContentJson.Error("trainer_profile", "$", "Unknown trainer source/profile or unsupported full-roster claim.");
        if (doc.Inputs.IsDefaultOrEmpty || doc.Limitations.IsDefaultOrEmpty || doc.Trainers.IsDefaultOrEmpty || doc.Battles.IsDefaultOrEmpty)
            throw ContentJson.Error("trainer_missing_data", "$", "Explicit inputs, limitations, rules, trainers and battles are required.");
        var inputs = new Dictionary<string, PretLabTrainerInput>(StringComparer.Ordinal);
        foreach (var input in doc.Inputs)
            if (input is null || input.Path is null || !ExpectedInputs.TryGetValue(input.Path, out var expected) ||
                input != expected || !inputs.TryAdd(input.Path, input))
                throw ContentJson.Error("trainer_source_identity", "$.inputs", "Input differs from the independently pinned source manifest.");
        if (inputs.Count != ExpectedInputs.Count)
            throw ContentJson.Error("trainer_source_missing", "$.inputs", "The complete bounded source manifest is required.");
        foreach (var trainer in doc.Trainers)
        {
            if (trainer is null || trainer.Party.IsDefaultOrEmpty)
                throw ContentJson.Error("trainer_party", "$.trainers", "Null trainer or missing party.");
            if (trainer.Id.Value != "pret:" + trainer.Source.Symbol)
                throw ContentJson.Error("trainer_symbol", "$.trainers", "Trainer ID must preserve its source symbol.");
            foreach (var member in trainer.Party)
            {
                if (member is null) throw ContentJson.Error("trainer_member", "$.trainers", "Null party member.");
                if (member.Species.Value != "pret:" + member.SpeciesNameSource.Symbol)
                    throw ContentJson.Error("trainer_species_symbol", "$.trainers", "Species ID must preserve its source symbol.");
            }
        }
        foreach (var battle in doc.Battles)
        {
            if (battle is null) throw ContentJson.Error("trainer_battle", "$.battles", "Null battle parameters.");
            if (battle.Trainer.Value != "pret:" + battle.Source.Symbol)
                throw ContentJson.Error("trainer_battle_symbol", "$.battles", "Battle must preserve its source trainer symbol.");
        }
        try
        {
            var catalog = new TrainerCatalog(doc.SourceCommit, doc.Profile, doc.CompleteSourceRoster,
                doc.Trainers.ToImmutableDictionary(t => t.Id), doc.Battles.ToImmutableDictionary(b => b.Trainer));
            TrainerPartyFactory.ValidateCatalog(catalog);
            return catalog;
        }
        catch (ArgumentException ex) { throw ContentJson.Error("trainer_catalog", "$", ex.Message); }
        catch (NotSupportedException ex) { throw ContentJson.Error("trainer_catalog", "$", ex.Message); }
    }

    // Byte identities only. The source data itself remains local and gitignored.
    private static readonly IReadOnlyDictionary<string, PretLabTrainerInput> ExpectedInputs =
        new Dictionary<string, PretLabTrainerInput>(StringComparer.Ordinal)
        {
            ["asm/macros/event.inc"] = new("asm/macros/event.inc"),["charmap.txt"] = new("charmap.txt"),["data/battle_ai_scripts.s"] = new("data/battle_ai_scripts.s"),["data/maps/PalletTown_ProfessorOaksLab/scripts.inc"] = new("data/maps/PalletTown_ProfessorOaksLab/scripts.inc"),["data/scripts/trainer_battle.inc"] = new("data/scripts/trainer_battle.inc"),["include/battle.h"] = new("include/battle.h"),["include/constants/battle.h"] = new("include/constants/battle.h"),["include/constants/battle_ai.h"] = new("include/constants/battle_ai.h"),["include/constants/battle_setup.h"] = new("include/constants/battle_setup.h"),["include/constants/items.h"] = new("include/constants/items.h"),["include/constants/moves.h"] = new("include/constants/moves.h"),["include/constants/opponents.h"] = new("include/constants/opponents.h"),["include/constants/pokemon.h"] = new("include/constants/pokemon.h"),["include/constants/species.h"] = new("include/constants/species.h"),["include/constants/trainers.h"] = new("include/constants/trainers.h"),["include/pokemon.h"] = new("include/pokemon.h"),["include/random.h"] = new("include/random.h"),["src/battle_ai_script_commands.c"] = new("src/battle_ai_script_commands.c"),["src/battle_controller_opponent.c"] = new("src/battle_controller_opponent.c"),["src/battle_main.c"] = new("src/battle_main.c"),["src/battle_setup.c"] = new("src/battle_setup.c"),["src/data/battle_moves.h"] = new("src/data/battle_moves.h"),["src/data/pokemon/level_up_learnsets.h"] = new("src/data/pokemon/level_up_learnsets.h"),["src/data/pokemon/species_info.h"] = new("src/data/pokemon/species_info.h"),["src/data/text/species_names.h"] = new("src/data/text/species_names.h"),["src/data/trainer_parties.h"] = new("src/data/trainer_parties.h"),["src/data/trainers.h"] = new("src/data/trainers.h"),["src/pokemon.c"] = new("src/pokemon.c"),["src/random.c"] = new("src/random.c")        };
}
