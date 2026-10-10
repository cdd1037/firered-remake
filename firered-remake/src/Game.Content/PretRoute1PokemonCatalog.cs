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
    public const string GeneratedSha256 = "81da3d737a389c783f7d6351cdefcdfd4ed830114bc437672a952b4e0fb0e06f";

    public static PokemonCatalog Load(string generatedJson) => LoadContent(generatedJson).Catalog;

    public static PretRoute1PokemonContent LoadContent(string generatedJson)
    {
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        var doc = ContentJson.ParseStrict<PretRoute1PokemonDocument>(generatedJson, options);
        if (doc.SchemaVersion != 1 || doc.Profile != Profile || doc.SourceCommit != SourceCommit)
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

        void Source(PokemonSourceRef source, string expectedPath)
        {
            if (source is null || source.Path != expectedPath || string.IsNullOrWhiteSpace(source.Symbol) ||
                source.StartLine < 1 || source.EndLine < source.StartLine ||
                !inputs.TryGetValue(source.Path, out var entry) || source.Sha256 != entry.Sha256)
                throw Error("source_ref", "Invalid source location or hash.");
        }

        var speciesIds = new HashSet<SpeciesId>();
        foreach (var species in doc.Species)
        {
            if (species is null || species.LevelUpMoves.IsDefaultOrEmpty || species.Evolutions.IsDefault ||
                !ExpectedSpecies.TryGetValue(species.Id, out int sourceId) || species.SourceSpeciesId != sourceId ||
                !speciesIds.Add(species.Id))
                throw Error("species", "Expected each of the fourteen explicitly pinned species exactly once.");
            Source(species.Source, "src/data/pokemon/species_info.h");
            Source(species.LearnsetSource, "src/data/pokemon/level_up_learnsets.h");
            if (species.Id.Value != "pret:" + species.Source.Symbol)
                throw Error("species_symbol", "Stable species ID must preserve the source symbol.");
            int previous = 0;
            foreach (var entry in species.LevelUpMoves)
            {
                if (entry is null || entry.Level is < 1 or > 100 || entry.Level < previous)
                    throw Error("learnset", "Expected ordered level-up entries.");
                Source(entry.Source, "src/data/pokemon/level_up_learnsets.h");
                if (entry.Source.Symbol != species.LearnsetSource.Symbol ||
                    entry.Source.StartLine < species.LearnsetSource.StartLine || entry.Source.EndLine > species.LearnsetSource.EndLine)
                    throw Error("learnset_source", "Learnset entry must be within the species source block.");
                previous = entry.Level;
            }
            foreach (var evolution in species.Evolutions)
            {
                if (evolution is null || evolution.Level is < 1 or > 100 || !ExpectedSpecies.ContainsKey(evolution.Target))
                    throw Error("evolution", "Invalid bounded level-evolution reference.");
                Source(evolution.Source, "src/data/pokemon/evolution.h");
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
            Source(move.Source, "src/data/battle_moves.h");
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
            Source(name.Source, "src/data/text/species_names.h");
            if (name.Id.Value != "pret:" + name.Source.Symbol)
                throw Error("species_name_symbol", "Name provenance must match its species.");
            speciesNames.Add(sourceId, name.Name);
        }
        var moveNames = ImmutableDictionary.CreateBuilder<MoveId, string>();
        foreach (var name in doc.MoveNames)
        {
            if (name is null || !moveIds.Contains(name.Id) || moveNames.ContainsKey(name.Id) || !CanonicalName(name.Name, 12))
                throw Error("move_name", "Invalid or duplicate canonical English move name.");
            Source(name.Source, "src/data/text/move_names.h");
            if (name.Id.Value != "pret:" + name.Source.Symbol)
                throw Error("move_name_symbol", "Name provenance must match its move.");
            moveNames.Add(name.Id, name.Name);
        }
        if (speciesNames.Count != speciesIds.Count || moveNames.Count != moveIds.Count)
            throw Error("names_missing", "Every imported species and move requires its canonical English name.");

        // Authenticity belongs here. A caller-supplied source-manifest claim cannot authenticate
        // altered statistics, PP, abilities, names, provenance lines or otherwise well-formed data.
        if (Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(generatedJson))) != GeneratedSha256)
            throw Error("generated_hash", "Generated bytes differ from the reviewed deterministic Route 1 artifact.");
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
            [new("pret:SPECIES_BULBASAUR")] = 1, [new("pret:SPECIES_IVYSAUR")] = 2,
            [new("pret:SPECIES_VENUSAUR")] = 3, [new("pret:SPECIES_CHARMANDER")] = 4,
            [new("pret:SPECIES_CHARMELEON")] = 5, [new("pret:SPECIES_CHARIZARD")] = 6,
            [new("pret:SPECIES_SQUIRTLE")] = 7, [new("pret:SPECIES_WARTORTLE")] = 8,
            [new("pret:SPECIES_BLASTOISE")] = 9, [new("pret:SPECIES_PIDGEY")] = 16,
            [new("pret:SPECIES_PIDGEOTTO")] = 17, [new("pret:SPECIES_PIDGEOT")] = 18,
            [new("pret:SPECIES_RATTATA")] = 19, [new("pret:SPECIES_RATICATE")] = 20
        };

    // Independently locked identities only; original tables remain ignored local artifacts.
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
            ["src/data/text/species_names.h"] = new("src/data/text/species_names.h", 15851, "25f2c9aa2363202e75cf2080be01465b2d64b5c4", "73e87117c98828423b7fc15565bd2a839a0671665fb7c1c1b26264d7d7a8912f"),
            ["src/data/text/move_names.h"] = new("src/data/text/move_names.h", 15252, "d63ddb871e07fab66ffcaf8ec051e86b54e1fe71", "d45a00ae94f33e294dd2fa742db1397fe1559fcf42ae553c8983f92d384b6bef"),
        };
}
