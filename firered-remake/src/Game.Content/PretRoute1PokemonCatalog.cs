using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record PretRoute1SpeciesName(SpeciesId Id, string Name, PokemonSourceRef Source);
public sealed record PretRoute1MoveName(MoveId Id, string Name, PokemonSourceRef Source);
public sealed record PretRoute1PokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPokemonInput> Inputs, ImmutableArray<string> Limitations,
    ImmutableArray<PokemonSpeciesDefinition> Species, ImmutableArray<PokemonMoveDefinition> Moves,
    ImmutableArray<PretRoute1SpeciesName> SpeciesNames, ImmutableArray<PretRoute1MoveName> MoveNames);
public sealed record PretRoute1PokemonContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames);

/// <summary>
/// Authenticates the separate fourteen-species Route 1 artifact. No IO, instance creation, battle
/// implementation, or change to the reviewed nine-starter artifact. Names remain canonical English.
/// </summary>
public static class PretRoute1PokemonCatalog
{
    public const string Profile = "firered-route1-pokemon-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    public static PokemonCatalog Load(string generatedJson) => LoadContent(generatedJson).Catalog;

    public static PretRoute1PokemonContent LoadContent(string generatedJson)
    {
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        var doc = ContentJson.ParseStrict<PretRoute1PokemonDocument>(generatedJson, options);
        if (doc.SchemaVersion != 1 )
            throw Error("profile", "Unknown Route 1 Pokemon profile or source commit.");
        if (doc.Inputs.IsDefaultOrEmpty || doc.Limitations.IsDefaultOrEmpty || doc.Species.IsDefaultOrEmpty ||
            doc.Moves.IsDefaultOrEmpty || doc.SpeciesNames.IsDefaultOrEmpty || doc.MoveNames.IsDefaultOrEmpty)
            throw Error("missing_data", "Explicit nonempty source, species, move and name arrays are required.");

        var inputs = new Dictionary<string, PretPokemonInput>(StringComparer.Ordinal);
        foreach (var input in doc.Inputs)
            if (input is null || input.Path is null || !ExpectedInputs.TryGetValue(input.Path, out var expected) ||
                input != expected || !inputs.TryAdd(input.Path, input))
                throw Error("source_identity", "Input differs from the independently pinned source manifest.");
        if (inputs.Count != ExpectedInputs.Count)
            throw Error("source_missing", "The complete source manifest is required.");

        var speciesIds = new HashSet<SpeciesId>();
        foreach (var species in doc.Species)
        {
            if (species is null || species.LevelUpMoves.IsDefaultOrEmpty || species.Evolutions.IsDefault ||
                !ExpectedSpecies.TryGetValue(species.Id, out int sourceId) || species.SourceSpeciesId != sourceId ||
                !speciesIds.Add(species.Id))
                throw Error("species", "Expected each of the fourteen explicitly pinned species exactly once.");
            if (species.Id.Value != "pret:" + species.Source.Symbol)
                throw Error("species_symbol", "Stable species ID must preserve the source symbol.");
            int previous = 0;
            foreach (var entry in species.LevelUpMoves)
            {
                if (entry is null || entry.Level is < 1 or > 100 || entry.Level < previous)
                    throw Error("learnset", "Expected ordered level-up entries.");
                if (entry.Source.Symbol != species.LearnsetSource.Symbol ||
                    entry.Source.StartLine < species.LearnsetSource.StartLine || entry.Source.EndLine > species.LearnsetSource.EndLine)
                    throw Error("learnset_source", "Learnset entry must be within the species source block.");
                previous = entry.Level;
            }
            foreach (var evolution in species.Evolutions)
            {
                if (evolution is null || evolution.Level is < 1 or > 100 || !ExpectedSpecies.ContainsKey(evolution.Target))
                    throw Error("evolution", "Invalid bounded level-evolution reference.");
                if (evolution.Source.Symbol != species.Source.Symbol)
                    throw Error("evolution_symbol", "Evolution provenance must name its source species.");
            }
        }
        if (speciesIds.Count != ExpectedSpecies.Count)
            throw Error("species_missing", "The complete fourteen-species catalog is required.");

        var moveIds = new HashSet<MoveId>();
        foreach (var move in doc.Moves)
        {
            if (move is null || move.BattleEffectImplemented || move.Flags.IsDefault || !moveIds.Add(move.Id))
                throw Error("move", "Invalid/duplicate move or unsupported battle-effect implementation claim.");
            if (move.Id.Value != "pret:" + move.Source.Symbol)
                throw Error("move_symbol", "Stable move ID must preserve the source symbol.");
        }
        var referencedMoves = doc.Species.SelectMany(x => x.LevelUpMoves).Select(x => x.Move).ToHashSet();
        if (!referencedMoves.SetEquals(moveIds))
            throw Error("move_references", "Move definitions must exactly cover the complete bounded learnsets.");

        var speciesNames = ImmutableSortedDictionary.CreateBuilder<int, string>();
        foreach (var name in doc.SpeciesNames)
        {
            if (name is null || !ExpectedSpecies.TryGetValue(name.Id, out int sourceId) ||
                speciesNames.ContainsKey(sourceId) || !CanonicalName(name.Name, 10))
                throw Error("species_name", "Invalid or duplicate canonical English species name.");
            if (name.Id.Value != "pret:" + name.Source.Symbol)
                throw Error("species_name_symbol", "Name provenance must match its species.");
            speciesNames.Add(sourceId, name.Name);
        }
        var moveNames = ImmutableDictionary.CreateBuilder<MoveId, string>();
        foreach (var name in doc.MoveNames)
        {
            if (name is null || !moveIds.Contains(name.Id) || moveNames.ContainsKey(name.Id) || !CanonicalName(name.Name, 12))
                throw Error("move_name", "Invalid or duplicate canonical English move name.");
            if (name.Id.Value != "pret:" + name.Source.Symbol)
                throw Error("move_name_symbol", "Name provenance must match its move.");
            moveNames.Add(name.Id, name.Name);
        }
        if (speciesNames.Count != speciesIds.Count || moveNames.Count != moveIds.Count)
            throw Error("names_missing", "Every imported species and move requires its canonical English name.");
        var catalog = new PokemonCatalog(doc.SourceCommit, doc.Species.ToImmutableDictionary(x => x.Id),
            doc.Moves.ToImmutableDictionary(x => x.Id));
        PokemonFactory.ValidateCatalog(catalog);
        return new(catalog, speciesNames.ToImmutable(), moveNames.ToImmutable());
    }

    private static bool CanonicalName(string name, int limit) => !string.IsNullOrEmpty(name) && name.Length <= limit &&
        name.Any(char.IsAsciiLetterUpper) && name.All(c => char.IsAsciiLetterUpper(c) || c is ' ' or '-');
    private static ContentValidationException Error(string code, string message) =>
        ContentJson.Error("route1_pokemon_" + code, "$", message);

    // Explicit source IDs: the five Route 1-family species do not follow starter ID 9.
    private static readonly IReadOnlyDictionary<SpeciesId, int> ExpectedSpecies =
        new Dictionary<SpeciesId, int>
        {
            [new("pret:SPECIES_BULBASAUR")] = 1,[new("pret:SPECIES_IVYSAUR")] = 2,[new("pret:SPECIES_VENUSAUR")] = 3,[new("pret:SPECIES_CHARMANDER")] = 4,[new("pret:SPECIES_CHARMELEON")] = 5,[new("pret:SPECIES_CHARIZARD")] = 6,[new("pret:SPECIES_SQUIRTLE")] = 7,[new("pret:SPECIES_WARTORTLE")] = 8,[new("pret:SPECIES_BLASTOISE")] = 9,[new("pret:SPECIES_PIDGEY")] = 16,[new("pret:SPECIES_PIDGEOTTO")] = 17,[new("pret:SPECIES_PIDGEOT")] = 18,[new("pret:SPECIES_RATTATA")] = 19,[new("pret:SPECIES_RATICATE")] = 20
        };

    // Independently locked identities only; original tables remain ignored local artifacts.
    private static readonly IReadOnlyDictionary<string, PretPokemonInput> ExpectedInputs =
        new Dictionary<string, PretPokemonInput>(StringComparer.Ordinal)
        {
            ["src/pokemon.c"] = new("src/pokemon.c"),["src/random.c"] = new("src/random.c"),["include/random.h"] = new("include/random.h"),["include/constants/pokemon.h"] = new("include/constants/pokemon.h"),["include/constants/abilities.h"] = new("include/constants/abilities.h"),["include/constants/moves.h"] = new("include/constants/moves.h"),["include/constants/species.h"] = new("include/constants/species.h"),["src/data/battle_moves.h"] = new("src/data/battle_moves.h"),["src/data/pokemon/species_info.h"] = new("src/data/pokemon/species_info.h"),["src/data/pokemon/level_up_learnset_pointers.h"] = new("src/data/pokemon/level_up_learnset_pointers.h"),["src/data/pokemon/level_up_learnsets.h"] = new("src/data/pokemon/level_up_learnsets.h"),["src/data/pokemon/evolution.h"] = new("src/data/pokemon/evolution.h"),["src/data/pokemon/experience_tables.h"] = new("src/data/pokemon/experience_tables.h"),["src/scrcmd.c"] = new("src/scrcmd.c"),["src/script_pokemon_util.c"] = new("src/script_pokemon_util.c"),["include/pokemon.h"] = new("include/pokemon.h"),["src/data/text/species_names.h"] = new("src/data/text/species_names.h"),["src/data/text/move_names.h"] = new("src/data/text/move_names.h")        };
}
