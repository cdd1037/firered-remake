using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Growth;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record PretPokemonInput(string Path);
public sealed record PretPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<string> Limitations,
    ImmutableArray<PokemonSpeciesDefinition> Species, ImmutableArray<PokemonMoveDefinition> Moves);

/// <summary>Strict, read-only loader for locally generated starter data. No filesystem or network access.</summary>
public static class PretPokemonCatalog
{
    public static PokemonCatalog Load(PretCanonicalPokemonCatalog canonical, string generatedJson)
    {
        var options = canonical.ReferenceOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        var doc = ContentJson.ParseStrict<PretPokemonDocument>(generatedJson, options);
        if (doc.SchemaVersion != 1 )
            throw ContentJson.Error("pokemon_profile", "$", "Unknown Pokemon profile or source commit.");
        if (doc.Limitations.IsDefaultOrEmpty || doc.Species.IsDefaultOrEmpty || doc.Moves.IsDefaultOrEmpty)
            throw ContentJson.Error("pokemon_missing_data", "$", "Explicit nonempty inputs, limitations, species and moves are required.");
        foreach (var species in doc.Species)
        {
            if (species is null || species.LevelUpMoves.IsDefault || species.Evolutions.IsDefault)
                throw ContentJson.Error("pokemon_species", "$.species", "Null species or missing arrays.");
            foreach (var entry in species.LevelUpMoves)
            {
                if (entry is null) throw ContentJson.Error("pokemon_learnset", "$.species", "Null learnset entry.");
            }
            foreach (var evolution in species.Evolutions)
            {
                if (evolution is null) throw ContentJson.Error("pokemon_evolution", "$.species", "Null evolution entry.");
            }
        }
        foreach (var move in doc.Moves)
        {
            if (move is null) throw ContentJson.Error("pokemon_move", "$.moves", "Null move.");
        }
        try
        {
            var catalog = new PokemonCatalog(canonical.Catalog.SourceCommit, doc.Species.ToImmutableDictionary(x => x.Id), doc.Moves.ToImmutableDictionary(x => x.Id));
            PokemonFactory.ValidateDefinitions(catalog);
            var used = catalog.Species.Values.SelectMany(x => x.LevelUpMoves).Select(x => x.Move).ToHashSet();
            if (used.Count != catalog.Moves.Count || catalog.Moves.Keys.Any(x => !used.Contains(x)))
                throw new ArgumentException("Unexpected or unused moves in bounded catalog.");
            return catalog;
        }
        catch (ArgumentException ex) { throw ContentJson.Error("pokemon_catalog", "$", ex.Message); }
        catch (NotSupportedException ex) { throw ContentJson.Error("pokemon_catalog", "$", ex.Message); }
    }

}
