using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record EeveePokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions,
    ImmutableArray<MoveId> RequiredMoves, int? NaturalLevelLimit, ImmutableArray<string> Limitations);

/// <summary>Authenticated source133 and Baton Pass metadata. All nine learn entries and five deferred evolutions remain source data.</summary>
public sealed class PretEeveePokemon
{
    public const string Profile = "firered-eevee-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string LockSha256 = "4d397c4ad8178088e26913b68cc18f9a8c0f830700197bf02577fa7e60090c0c";
    public const string CatalogSha256 = "864f4d9cc672d2645dd2323e11c01bf0092922eaa01be4f8eb725ea17b08ca5b";
    public const string CatalogRelativePath = "content/generated/eevee-pokemon/catalog.json";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public EeveePokemonDocument Document { get; }
    public ImmutableSortedDictionary<int, string> AdditionalSpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> AdditionalMoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions => Document.DeferredEvolutions;
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }

    private PretEeveePokemon(PokemonCatalog catalog, EeveePokemonDocument document,
        ImmutableArray<PretWorldSourceFile> files)
    {
        Catalog = catalog; Document = document; SourceFiles = files;
        AdditionalSpeciesNames = document.SpeciesNames.ToImmutableSortedDictionary(n => catalog.Species[n.Id].SourceSpeciesId, n => n.Name);
        AdditionalMoveNames = document.MoveNames.ToImmutableDictionary(n => n.Id, n => n.Name);
        Constants = document.Constants.ToImmutableDictionary(c => c.Symbol, c => c.Value, StringComparer.Ordinal);
        EncodedNames = document.EncodedNames.ToImmutableDictionary(n => n.Symbol, StringComparer.Ordinal);
        AdditionalSourceHashes = ["eevee-pokemon-lock:" + LockSha256, "eevee-pokemon-catalog:" + CatalogSha256,
            .. files.Select(f => "eevee-pokemon:" + f.Path + ":" + f.Sha256)];
    }

    /// <summary>Composes102/240 campaign or102/239 standalone into103/241 or103/240; keeps inherited objects.</summary>
    public static PretEeveePokemon Load(PokemonCatalog baseCatalog, string projectRoot, string? dependencyProjectRoot = null)
    {
        PokemonFactory.ValidateCatalog(baseCatalog);
        Require(baseCatalog.SourceCommit == SourceCommit && baseCatalog.Species.Count == 102 &&
            baseCatalog.Moves.Count is 239 or 240, "Eevee predecessor scope differs.");
        var project = Path.GetFullPath(projectRoot);
        // All new source bytes are self-contained; dependencyProjectRoot is retained for composition API consistency.
        var bytes = PretCeladonApproachSource.Read(project, CatalogRelativePath, CatalogSha256, 18631);
        _ = PretCeladonApproachSource.Read(project, "scripts/eevee-pokemon.lock.json", LockSha256, 7991);
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<EeveePokemonDocument>(Utf8.GetString(bytes), options);
        Require(doc.SchemaVersion == 1 && doc.Profile == Profile && doc.SourceCommit == SourceCommit &&
            doc.Inputs.Length == 33 && doc.Species.Length == 1 && doc.Moves.Length == 1 &&
            doc.SpeciesNames.Length == 1 && doc.MoveNames.Length == 1 && doc.EncodedNames.Length == 2 &&
            doc.Constants.Length == 19 && doc.DeferredEvolutions.Length == 5 && doc.RequiredMoves.Length == 9 &&
            doc.NaturalLevelLimit is null && !doc.Limitations.IsDefaultOrEmpty, "Eevee document scope differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var lineCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var input in doc.Inputs)
        {
            var source = PretCeladonApproachSource.Read(project, "content/raw/eevee-pokemon/" + input.Path, input.Sha256, input.ByteLength);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {source.Length}\0"), .. source])) == input.GitBlobSha1,
                "Eevee source Git blob differs: " + input.Path);
            if (!input.Path.EndsWith(".png", StringComparison.Ordinal)) lineCounts.Add(input.Path, Utf8.GetString(source).Count(c => c == '\n') + 1);
            files.Add(new(input.Path, input.GitBlobSha1, input.ByteLength, input.Sha256,
                $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{input.Path}"));
        }
        void Source(PokemonSourceRef r) => Require(doc.Inputs.Any(f => f.Path == r.Path && f.Sha256 == r.Sha256) &&
            r.StartLine > 0 && r.EndLine >= r.StartLine && lineCounts.TryGetValue(r.Path, out int lines) && r.EndLine <= lines &&
            !string.IsNullOrWhiteSpace(r.Symbol), "Eevee source reference differs.");
        var s = doc.Species.Single();
        Require(Game.Domain.WildBattle.EeveeBattleScope.IsEeveeDefinition(s), "Eevee source species identity differs.");
        var expectedEvolutions = new[] { ("EVO_ITEM","ITEM_THUNDER_STONE","JOLTEON"),("EVO_ITEM","ITEM_WATER_STONE","VAPOREON"),
            ("EVO_ITEM","ITEM_FIRE_STONE","FLAREON"),("EVO_FRIENDSHIP_DAY","0","ESPEON"),("EVO_FRIENDSHIP_NIGHT","0","UMBREON") };
        Require(doc.DeferredEvolutions.Select(e => (e.Method,e.Parameter,e.Target.Value)).SequenceEqual(
            expectedEvolutions.Select(e => (e.Item1,e.Item2,"pret:SPECIES_"+e.Item3))) &&
            doc.DeferredEvolutions.All(e => e.Species==s.Id && !e.Executable && !string.IsNullOrWhiteSpace(e.Reason)),
            "Eevee deferred source evolution rows differ.");
        foreach (var e in doc.DeferredEvolutions) Source(e.Source);
        Source(s.Source); Source(s.LearnsetSource); foreach (var e in s.LevelUpMoves) Source(e.Source);
        var species = baseCatalog.Species.Add(s.Id,s); var moves = baseCatalog.Moves.ToBuilder();
        Require(doc.Moves.Select(m => m.SourceMoveId).ToHashSet().SetEquals(new[] {226}), "Eevee new move identities differ.");
        foreach (var m in doc.Moves) { Source(m.Source); Require(!m.BattleEffectImplemented, "Metadata cannot admit effects."); moves.Add(m.Id,m); }
        Require(doc.RequiredMoves.Distinct().Count() == 9 && doc.RequiredMoves.All(moves.ContainsKey), "Eevee move closure differs.");
        foreach (var n in doc.SpeciesNames) Source(n.Source);
        foreach (var n in doc.MoveNames) Source(n.Source);
        foreach (var c in doc.Constants) Source(c.Source);
        foreach (var n in doc.EncodedNames)
        {
            Source(n.Source); Source(n.EncodingSource);
            Require(n.Bytes.Length == n.Name.Length + 1 && n.Bytes[^1] == 255 && n.Bytes.Take(n.Bytes.Length-1).All(b => b is >=0 and <255),
                "Eevee encoded name differs.");
        }
        var catalog = new PokemonCatalog(SourceCommit,species,moves.ToImmutable());
        Require(catalog.Species.Count == 103 && catalog.Moves.Count == baseCatalog.Moves.Count + 1 &&
            baseCatalog.Species.All(p => ReferenceEquals(p.Value,catalog.Species[p.Key])) &&
            baseCatalog.Moves.All(p => ReferenceEquals(p.Value,catalog.Moves[p.Key])), "Eevee inherited records differ.");
        // The battle/domain increment owns the exact103-species factory admission.
        PokemonFactory.ValidateCatalog(catalog);
        return new(catalog,doc,files.ToImmutable());
    }
    private static void Require(bool value,string message) { if (!value) throw new InvalidDataException(message); }
}
