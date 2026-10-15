using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Growth;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record PretPokemonInput(string Path);
public sealed record PretPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPokemonInput> Inputs, ImmutableArray<string> Limitations,
    ImmutableArray<PokemonSpeciesDefinition> Species, ImmutableArray<PokemonMoveDefinition> Moves);

/// <summary>Strict, read-only loader for locally generated starter data. No filesystem or network access.</summary>
public static class PretPokemonCatalog
{
    public static PokemonCatalog Load(string generatedJson)
    {
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        var doc = ContentJson.ParseStrict<PretPokemonDocument>(generatedJson, options);
        if (doc.SchemaVersion != 1 )
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
        foreach (var species in doc.Species)
        {
            if (species is null || species.LevelUpMoves.IsDefault || species.Evolutions.IsDefault)
                throw ContentJson.Error("pokemon_species", "$.species", "Null species or missing arrays.");
            if (species.Id.Value != "pret:" + species.Source.Symbol)
                throw ContentJson.Error("pokemon_species_symbol", "$.species", "Stable ID must preserve the source species symbol.");
            foreach (var entry in species.LevelUpMoves)
            {
                if (entry is null) throw ContentJson.Error("pokemon_learnset", "$.species", "Null learnset entry.");
                if (entry.Source.Symbol != species.LearnsetSource.Symbol || entry.Source.StartLine < species.LearnsetSource.StartLine || entry.Source.EndLine > species.LearnsetSource.EndLine)
                    throw ContentJson.Error("pokemon_learnset_source", "$.species", "Learnset entry must be inside its species learnset source.");
            }
            foreach (var evolution in species.Evolutions)
            {
                if (evolution is null) throw ContentJson.Error("pokemon_evolution", "$.species", "Null evolution entry.");
            }
        }
        foreach (var move in doc.Moves)
        {
            if (move is null) throw ContentJson.Error("pokemon_move", "$.moves", "Null move.");
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
            return catalog;
        }
        catch (ArgumentException ex) { throw ContentJson.Error("pokemon_catalog", "$", ex.Message); }
        catch (NotSupportedException ex) { throw ContentJson.Error("pokemon_catalog", "$", ex.Message); }
    }

    // Hashes/identities only: no game tables are embedded in source control.
    private static readonly IReadOnlyDictionary<string, PretPokemonInput> ExpectedInputs =
        new Dictionary<string, PretPokemonInput>(StringComparer.Ordinal)
        {
            ["src/pokemon.c"] = new("src/pokemon.c"),["src/random.c"] = new("src/random.c"),["include/random.h"] = new("include/random.h"),["include/constants/pokemon.h"] = new("include/constants/pokemon.h"),["include/constants/abilities.h"] = new("include/constants/abilities.h"),["include/constants/moves.h"] = new("include/constants/moves.h"),["include/constants/species.h"] = new("include/constants/species.h"),["src/data/battle_moves.h"] = new("src/data/battle_moves.h"),["src/data/pokemon/species_info.h"] = new("src/data/pokemon/species_info.h"),["src/data/pokemon/level_up_learnset_pointers.h"] = new("src/data/pokemon/level_up_learnset_pointers.h"),["src/data/pokemon/level_up_learnsets.h"] = new("src/data/pokemon/level_up_learnsets.h"),["src/data/pokemon/evolution.h"] = new("src/data/pokemon/evolution.h"),["src/data/pokemon/experience_tables.h"] = new("src/data/pokemon/experience_tables.h"),["src/scrcmd.c"] = new("src/scrcmd.c"),["src/script_pokemon_util.c"] = new("src/script_pokemon_util.c"),["include/pokemon.h"] = new("include/pokemon.h")        };
}
