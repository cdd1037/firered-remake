using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record PokemonTowerPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions,
    ImmutableArray<MoveId> RequiredMoves, int? NaturalLevelLimit, ImmutableArray<string> Limitations);

/// <summary>
/// Source-authenticated Gastly/Haunter and five absent move definitions. Source bytes are reused from
/// the Rocket catalog; no inherited record, battle admission, artwork or audio is changed here.
/// </summary>
public sealed class PretPokemonTowerPokemon
{
    public const string Profile = "firered-pokemon-tower-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string CatalogSha256 = "a5e8753657f1c017dd89823b2667f51a5f3e47d24ba1b16475230d99b4edc595";
    public const string CatalogRelativePath = "content/generated/pokemon-tower-pokemon/catalog.json";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public PokemonCatalog Catalog { get; }
    public PokemonTowerPokemonDocument Document { get; }
    // These are additions to the caller's inherited name/evolution collections, not replacements.
    public ImmutableSortedDictionary<int, string> AdditionalSpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> AdditionalMoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions => Document.DeferredEvolutions;
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }

    private PretPokemonTowerPokemon(PokemonCatalog catalog, PokemonTowerPokemonDocument document,
        ImmutableArray<PretWorldSourceFile> files)
    {
        Catalog = catalog;
        Document = document;
        SourceFiles = files;
        AdditionalSpeciesNames = document.SpeciesNames.ToImmutableSortedDictionary(
            n => catalog.Species[n.Id].SourceSpeciesId, n => n.Name);
        AdditionalMoveNames = document.MoveNames.ToImmutableDictionary(n => n.Id, n => n.Name);
        Constants = document.Constants.ToImmutableDictionary(c => c.Symbol, c => c.Value, StringComparer.Ordinal);
        EncodedNames = document.EncodedNames.ToImmutableDictionary(n => n.Symbol, StringComparer.Ordinal);
        AdditionalSourceHashes = ["pokemon-tower-pokemon-catalog:" + CatalogSha256,
            .. files.Select(f => "pokemon-tower-pokemon:" + f.Path + ":" + f.Sha256)];
    }

    /// <summary>
    /// Overlay an authenticated 99-species predecessor. Pass the campaign catalog to retain its
    /// Shock Wave supplement (231 → 236 moves); the standalone Rocket catalog composes 230 → 235.
    /// </summary>
    public static PretPokemonTowerPokemon Load(PokemonCatalog baseCatalog, string projectRoot,
        string? dependencyProjectRoot = null)
    {
        PokemonFactory.ValidateCatalog(baseCatalog);
        Require(baseCatalog.SourceCommit == SourceCommit && baseCatalog.Species.Count == 99 &&
            baseCatalog.Moves.Count is 230 or 231, "Pokemon Tower predecessor scope differs.");
        var project = Path.GetFullPath(projectRoot);
        var dependency = Path.GetFullPath(dependencyProjectRoot ?? project);
        var bytes = PretCeladonApproachSource.Read(project, CatalogRelativePath, CatalogSha256, 24331);
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<PokemonTowerPokemonDocument>(Utf8.GetString(bytes), options);
        Require(doc.SchemaVersion == 1 && doc.Profile == Profile && doc.SourceCommit == SourceCommit &&
            doc.Inputs.Length == 14 && doc.Species.Length == 2 && doc.Moves.Length == 5 &&
            doc.SpeciesNames.Length == 2 && doc.MoveNames.Length == 5 && doc.EncodedNames.Length == 7 &&
            doc.Constants.Length == 21 && doc.DeferredEvolutions.Length == 1 && doc.RequiredMoves.Length == 13 &&
            doc.NaturalLevelLimit is null && !doc.Limitations.IsDefaultOrEmpty,
            "Pokemon Tower document scope differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var lineCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var input in doc.Inputs)
        {
            var source = PretCeladonApproachSource.Read(dependency,
                "content/raw/rocket-hideout-pokemon/" + input.Path, input.Sha256, input.ByteLength);
            Require(Convert.ToHexStringLower(SHA1.HashData([
                .. Encoding.ASCII.GetBytes($"blob {source.Length}\0"), .. source])) == input.GitBlobSha1,
                "Pokemon Tower source Git blob differs: " + input.Path);
            lineCounts.Add(input.Path, Utf8.GetString(source).Count(c => c == '\n') + 1);
            files.Add(new(input.Path, input.GitBlobSha1, input.ByteLength, input.Sha256,
                $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{input.Path}"));
        }
        void Source(PokemonSourceRef reference)
        {
            Require(doc.Inputs.Any(f => f.Path == reference.Path && f.Sha256 == reference.Sha256) &&
                reference.StartLine > 0 && reference.EndLine >= reference.StartLine &&
                lineCounts.TryGetValue(reference.Path, out int lines) && reference.EndLine <= lines &&
                !string.IsNullOrWhiteSpace(reference.Symbol), "Pokemon Tower source reference differs.");
        }
        var species = baseCatalog.Species.ToBuilder();
        var expected = new[] { ("GASTLY", 92, 11), ("HAUNTER", 93, 13) };
        for (int index = 0; index < expected.Length; index++)
        {
            var s = doc.Species[index];
            var e = expected[index];
            Require(s.Id.Value == "pret:SPECIES_" + e.Item1 && s.SourceSpeciesId == e.Item2 &&
                s.LevelUpMoves.Length == e.Item3 && s.Type1 == "TYPE_GHOST" && s.Type2 == "TYPE_POISON" &&
                s.Ability1 == "ABILITY_LEVITATE" && s.Ability2 == "ABILITY_NONE" && s.GenderRatio == 127 &&
                s.GrowthRate == GrowthRate.MediumSlow && s.Friendship == 70,
                "Pokemon Tower species identity differs.");
            Source(s.Source);
            Source(s.LearnsetSource);
            foreach (var entry in s.LevelUpMoves) Source(entry.Source);
            foreach (var evolution in s.Evolutions) Source(evolution.Source);
            species.Add(s.Id, s);
        }
        var gastlyEvolution = doc.Species[0].Evolutions.Single();
        Require(gastlyEvolution.Level == 25 && gastlyEvolution.Method == PokemonEvolutionMethod.Level &&
            gastlyEvolution.Target.Value == "pret:SPECIES_HAUNTER" && gastlyEvolution.Parameter is null &&
            doc.Species[1].Evolutions.IsEmpty, "Pokemon Tower level evolution differs.");
        var deferred = doc.DeferredEvolutions.Single();
        Source(deferred.Source);
        Require(deferred.Species.Value == "pret:SPECIES_HAUNTER" && deferred.Target.Value == "pret:SPECIES_GENGAR" &&
            deferred.Method == "EVO_TRADE" && deferred.Parameter == "0" && !deferred.Executable &&
            !species.ContainsKey(new("pret:SPECIES_GENGAR")), "Pokemon Tower deferred trade evolution differs.");
        var moves = baseCatalog.Moves.ToBuilder();
        foreach (var m in doc.Moves)
        {
            Source(m.Source);
            Require(!m.BattleEffectImplemented, "Pokemon Tower metadata cannot advertise effect execution.");
            moves.Add(m.Id, m);
        }
        Require(doc.RequiredMoves.Distinct().Count() == 13 && doc.RequiredMoves.All(moves.ContainsKey) &&
            species.Count == 101 && moves.Count == baseCatalog.Moves.Count + 5,
            "Pokemon Tower species/move closure differs.");
        foreach (var n in doc.SpeciesNames) Source(n.Source);
        foreach (var n in doc.MoveNames) Source(n.Source);
        foreach (var c in doc.Constants) Source(c.Source);
        foreach (var n in doc.EncodedNames)
        {
            Source(n.Source);
            Source(n.EncodingSource);
            Require(n.Bytes.Length == n.Name.Length + 1 && n.Bytes[^1] == 255 &&
                n.Bytes.Take(n.Bytes.Length - 1).All(b => b is >= 0 and < 255),
                "Pokemon Tower encoded name differs.");
        }
        var catalog = new PokemonCatalog(SourceCommit, species.ToImmutable(), moves.ToImmutable());
        Require(baseCatalog.Species.All(p => ReferenceEquals(p.Value, catalog.Species[p.Key])) &&
            baseCatalog.Moves.All(p => ReferenceEquals(p.Value, catalog.Moves[p.Key])),
            "Pokemon Tower replaced an inherited record.");
        // The integration owner must add the corresponding 101-species factory admission.
        PokemonFactory.ValidateCatalog(catalog);
        return new(catalog, doc, files.ToImmutable());
    }

    /// <summary>Convenience loader for the standalone 230-move Rocket catalog.</summary>
    public static PretPokemonTowerPokemon Load(string projectRoot, string? dependencyProjectRoot = null)
    {
        var inherited = PretRocketHideoutPokemon.Load(dependencyProjectRoot ?? projectRoot);
        return Load(inherited.Catalog, projectRoot, dependencyProjectRoot);
    }

    private static void Require(bool condition, string error)
    {
        if (!condition) throw new InvalidDataException(error);
    }
}
