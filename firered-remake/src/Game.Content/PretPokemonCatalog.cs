using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Growth;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record PretPokemonInput(string Path, int ByteLength, string GitBlobSha1, string Sha256);
public sealed record PretPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPokemonInput> Inputs, ImmutableArray<string> Limitations,
    ImmutableArray<PokemonSpeciesDefinition> Species, ImmutableArray<PokemonMoveDefinition> Moves);

/// <summary>Strict, read-only loader for locally generated starter data. No filesystem or network access.</summary>
public static class PretPokemonCatalog
{
    // Deterministic UTF-8/LF output of scripts/import-pokemon.py for the locked inputs.
    // A source-manifest claim alone must not authenticate changed stats or learnsets.
    public const string GeneratedSha256 = "396cb44028e74884c3ddc14b06ae054f297b24fa4606dee420998c78d60b27b0";
    public static PokemonCatalog Load(string generatedJson)
    {
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        var doc = ContentJson.ParseStrict<PretPokemonDocument>(generatedJson, options);
        if (doc.SchemaVersion != 1 || doc.Profile != PokemonFactory.RulesVersion || doc.SourceCommit != PokemonFactory.SourceCommit)
            throw ContentJson.Error("pokemon_profile", "$", "Unknown Pokemon profile or source commit.");
        if (doc.Inputs.IsDefaultOrEmpty || doc.Limitations.IsDefaultOrEmpty || doc.Species.IsDefaultOrEmpty || doc.Moves.IsDefaultOrEmpty)
            throw ContentJson.Error("pokemon_missing_data", "$", "Explicit nonempty inputs, limitations, species and moves are required.");
        var inputs = new Dictionary<string, PretPokemonInput>(StringComparer.Ordinal);
        foreach (var input in doc.Inputs)
        {
            if (input is null || input.Path is null || !ExpectedInputs.TryGetValue(input.Path, out var expected) || input != expected || !inputs.TryAdd(input.Path, input))
                throw ContentJson.Error("pokemon_source_identity", "$.inputs", "Input does not match the independently pinned file manifest.");
        }
        if (inputs.Count != ExpectedInputs.Count)
            throw ContentJson.Error("pokemon_source_missing", "$.inputs", "The complete source manifest is required.");
        void Source(PokemonSourceRef source, string expectedPath)
        {
            if (source is null || source.Path != expectedPath || string.IsNullOrWhiteSpace(source.Symbol) ||
                source.StartLine < 1 || source.EndLine < source.StartLine ||
                !inputs.TryGetValue(source.Path, out var entry) || source.Sha256 != entry.Sha256)
                throw ContentJson.Error("pokemon_source_ref", "$", "Invalid source location or hash.");
        }
        foreach (var species in doc.Species)
        {
            if (species is null || species.LevelUpMoves.IsDefault || species.Evolutions.IsDefault)
                throw ContentJson.Error("pokemon_species", "$.species", "Null species or missing arrays.");
            Source(species.Source, "src/data/pokemon/species_info.h");
            Source(species.LearnsetSource, "src/data/pokemon/level_up_learnsets.h");
            if (species.Id.Value != "pret:" + species.Source.Symbol)
                throw ContentJson.Error("pokemon_species_symbol", "$.species", "Stable ID must preserve the source species symbol.");
            foreach (var entry in species.LevelUpMoves)
            {
                if (entry is null) throw ContentJson.Error("pokemon_learnset", "$.species", "Null learnset entry.");
                Source(entry.Source, "src/data/pokemon/level_up_learnsets.h");
                if (entry.Source.Symbol != species.LearnsetSource.Symbol || entry.Source.StartLine < species.LearnsetSource.StartLine || entry.Source.EndLine > species.LearnsetSource.EndLine)
                    throw ContentJson.Error("pokemon_learnset_source", "$.species", "Learnset entry must be inside its species learnset source.");
            }
            foreach (var evolution in species.Evolutions)
            {
                if (evolution is null) throw ContentJson.Error("pokemon_evolution", "$.species", "Null evolution entry.");
                Source(evolution.Source, "src/data/pokemon/evolution.h");
            }
        }
        foreach (var move in doc.Moves)
        {
            if (move is null) throw ContentJson.Error("pokemon_move", "$.moves", "Null move.");
            Source(move.Source, "src/data/battle_moves.h");
            if (move.Id.Value != "pret:" + move.Source.Symbol)
                throw ContentJson.Error("pokemon_move_symbol", "$.moves", "Stable ID must preserve the source move symbol.");
        }
        try
        {
            var catalog = new PokemonCatalog(doc.SourceCommit, doc.Species.ToImmutableDictionary(x => x.Id), doc.Moves.ToImmutableDictionary(x => x.Id));
            PokemonFactory.ValidateCatalog(catalog);
            var used = catalog.Species.Values.SelectMany(x => x.LevelUpMoves).Select(x => x.Move).ToHashSet();
            if (used.Count != catalog.Moves.Count || catalog.Moves.Keys.Any(x => !used.Contains(x)))
                throw new ArgumentException("Unexpected or unused moves in bounded catalog.");
            var actualHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(generatedJson)));
            if (actualHash != GeneratedSha256)
                throw ContentJson.Error("pokemon_generated_hash", "$", "Generated bytes differ from the reviewed deterministic starter catalog. Regenerate from the locked inputs.");
            return catalog;
        }
        catch (ArgumentException ex) { throw ContentJson.Error("pokemon_catalog", "$", ex.Message); }
        catch (NotSupportedException ex) { throw ContentJson.Error("pokemon_catalog", "$", ex.Message); }
    }

    // Hashes/identities only: no game tables are embedded in source control.
    private static readonly IReadOnlyDictionary<string, PretPokemonInput> ExpectedInputs =
        new Dictionary<string, PretPokemonInput>(StringComparer.Ordinal)
        {
            ["src/pokemon.c"] = new("src/pokemon.c", 221865, "66a5c3d233e8b1b85fe5045aeb6667332cd61d4c", "116a470cd9ebfa226aa8d85c2eb63591e176d726837c67baaa1ac6260d50f2ec"),
            ["src/random.c"] = new("src/random.c", 323, "635a1df5a59fa42a88f30ad884405db2bde95ea5", "e7e90cc070d1fbf09579f5f91762cd8875d5bbe1d7a90f468bcd870df73b9de2"),
            ["include/random.h"] = new("include/random.h", 669, "fd6b88ad0af62f22951e6e81fa21e38b12480ece", "8a6049183598b2d865b5ff522abdb3d0e94012a127d724af46b9b7b69803f5b9"),
            ["include/constants/pokemon.h"] = new("include/constants/pokemon.h", 10894, "21c678744f21693825a0c03bb73efa77a256af8b", "9cabd203100ae7246bc595b7295e7e04145f6292abfab83c3bd0597a2cfbcfb5"),
            ["include/constants/abilities.h"] = new("include/constants/abilities.h", 2407, "4ad54c86fa1c7cac4abe49d89adaa0ce1cc82d98", "1c1b93de5d5ff65d8b3066e9b40229b5d30e02842e587e3851a1d708ece1292b"),
            ["include/constants/moves.h"] = new("include/constants/moves.h", 10249, "14738f1b21597269ba1934762e437dec3b4cc643", "79d5b93423006c25e6ee47355b15f403b2534616fd6b638e1755e5886d378194"),
            ["include/constants/species.h"] = new("include/constants/species.h", 12982, "7446cc1380995ab62deff8359bad813aa2b936e9", "ec3f9f9c2717dcd453aa7a27506c32e1e6ca067864534dee494695d74344c51a"),
            ["src/data/battle_moves.h"] = new("src/data/battle_moves.h", 121457, "c6457675d5441010feebbb5aec58dc9e37356134", "54e08c749547afaf73eb2f1e4f4e3023f4d8e186d2f608c29bd6419c291b5384"),
            ["src/data/pokemon/species_info.h"] = new("src/data/pokemon/species_info.h", 342600, "3e36c08ced6dec3d427673d8b2f787221a178aa2", "9c2da1284f7859700b315abda0499334c422f2c6776d576c788fcd1e4050f71e"),
            ["src/data/pokemon/level_up_learnset_pointers.h"] = new("src/data/pokemon/level_up_learnset_pointers.h", 20597, "661458ce383ef517cea82c637bfb99536b6f65c3", "7a85513797da2a2ebdf7893ce58d94f99aee07deb1aec4f0d7a31422dc8040de"),
            ["src/data/pokemon/level_up_learnsets.h"] = new("src/data/pokemon/level_up_learnsets.h", 182027, "bdf1f9fbb9ad2791fcaec31826dc30d180fabbe0", "d6563a88fa893d1433a02027cb13ba9574a6828a96b1a730b1d767fd7c5a0cc6"),
            ["src/data/pokemon/evolution.h"] = new("src/data/pokemon/evolution.h", 12282, "c9b0cf716d6e25cfa054e988115af60f61bd206e", "a7d05352fb5b7039b8851049bc9575dc8eaa4450bf9964d03b7857db98c32d3c"),
            ["src/data/pokemon/experience_tables.h"] = new("src/data/pokemon/experience_tables.h", 22610, "8f50e10918569a7b45045aa4f8067bac75d36911", "9a236761640676e251b76b7950b86e5881ee23dfe920ca27bbe47b4541542731"),
            ["src/scrcmd.c"] = new("src/scrcmd.c", 56954, "97b695ee2f6fca80a3b78f52f6a444f696fe5814", "898dad5a07ce0a125731b998654d86808885a8d48163d607478a5e70c1cac553"),
            ["src/script_pokemon_util.c"] = new("src/script_pokemon_util.c", 5764, "bc11dc001b4399ecd25b8699e316816e1ebd8058", "cfadee8f8c397485b66fc56a9a6e13fef1bbad47b4c6cd83dd42a9c1f8f9cf30"),
            ["include/pokemon.h"] = new("include/pokemon.h", 16352, "414dc41767295aed38f299e8495c5e2f8d25ddc9", "a6881fee07ae10f57ebf3ec7af1cabd2e900d8ba8d3da644313c087d16e1617e"),
        };
}
