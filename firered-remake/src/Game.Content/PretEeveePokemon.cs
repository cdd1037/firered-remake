using System.Collections.Immutable;
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

    private PretEeveePokemon(PokemonCatalog catalog, EeveePokemonDocument document,
        ImmutableArray<PretWorldSourceFile> files)
    {
        Catalog = catalog; Document = document; SourceFiles = files;
        AdditionalSpeciesNames = document.SpeciesNames.ToImmutableSortedDictionary(n => catalog.Species[n.Id].SourceSpeciesId, n => n.Name);
        AdditionalMoveNames = document.MoveNames.ToImmutableDictionary(n => n.Id, n => n.Name);
        Constants = document.Constants.ToImmutableDictionary(c => c.Symbol, c => c.Value, StringComparer.Ordinal);
        EncodedNames = document.EncodedNames.ToImmutableDictionary(n => n.Symbol, StringComparer.Ordinal);
    }

    /// <summary>Composes102/240 campaign or102/239 standalone into103/241 or103/240; keeps inherited objects.</summary>
    public static PretEeveePokemon Load(PokemonCatalog baseCatalog, string projectRoot, string? dependencyProjectRoot = null)
    {
        PokemonFactory.ValidateCatalog(baseCatalog);
        Require(baseCatalog.Species.Count == 102 &&
            baseCatalog.Moves.Count is 239 or 240, "Eevee predecessor scope differs.");
        var project = Path.GetFullPath(projectRoot);
        // All new source bytes are self-contained; dependencyProjectRoot is retained for composition API consistency.
        var bytes = PretCeladonApproachSource.Read(project,CatalogRelativePath);
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<EeveePokemonDocument>(Utf8.GetString(bytes), options);
        Require(doc.SchemaVersion == 1 && doc.DeferredEvolutions.Length == 5 && doc.RequiredMoves.Length == 9 &&
            doc.NaturalLevelLimit is null && !doc.Limitations.IsDefaultOrEmpty, "Eevee document scope differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var lineCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var input in doc.Inputs)
        {
            var source = PretCeladonApproachSource.Read(project,PretSourcePaths.FireRedRawDirectory + "/" + input.Path);
            if (!input.Path.EndsWith(".png", StringComparison.Ordinal)) lineCounts.Add(input.Path, Utf8.GetString(source).Count(c => c == '\n') + 1);
            files.Add(new(input.Path,                $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{input.Path}"));
        }
        var s = doc.Species.Single();
        Require(Game.Domain.WildBattle.EeveeBattleScope.IsEeveeDefinition(s), "Eevee source species identity differs.");
        var expectedEvolutions = new[] { ("EVO_ITEM","ITEM_THUNDER_STONE","JOLTEON"),("EVO_ITEM","ITEM_WATER_STONE","VAPOREON"),("EVO_ITEM","ITEM_FIRE_STONE","FLAREON"),("EVO_FRIENDSHIP_DAY","0","ESPEON"),("EVO_FRIENDSHIP_NIGHT","0","UMBREON") };
        Require(doc.DeferredEvolutions.Select(e => (e.Method,e.Parameter,e.Target.Value)).SequenceEqual(
            expectedEvolutions.Select(e => (e.Item1,e.Item2,"pret:SPECIES_"+e.Item3))) &&
            doc.DeferredEvolutions.All(e => e.Species==s.Id && !e.Executable && !string.IsNullOrWhiteSpace(e.Reason)),
            "Eevee deferred source evolution rows differ.");
        var species = baseCatalog.Species.Add(s.Id,s); var moves = baseCatalog.Moves.ToBuilder();
        Require(doc.Moves.Select(m => m.SourceMoveId).ToHashSet().SetEquals(new[] {226}), "Eevee new move identities differ.");
        foreach (var m in doc.Moves) { Require(!m.BattleEffectImplemented, "Metadata cannot admit effects."); moves.Add(m.Id,m); }
        Require(doc.RequiredMoves.Distinct().Count() == 9 && doc.RequiredMoves.All(moves.ContainsKey), "Eevee move closure differs.");
        foreach (var n in doc.EncodedNames)
        {
            Require(n.Bytes.Length == n.Name.Length + 1 && n.Bytes[^1] == 255 && n.Bytes.Take(n.Bytes.Length-1).All(b => b is >=0 and <255),
                "Eevee encoded name differs.");
        }
        var catalog = new PokemonCatalog(SourceCommit,species,moves.ToImmutable());
        Require(catalog.Species.Count == 103 && catalog.Moves.Count == baseCatalog.Moves.Count + 1 , "Eevee inherited records differ.");
        // The battle/domain increment owns the exact103-species factory admission.
        PokemonFactory.ValidateCatalog(catalog);
        return new(catalog,doc,files.ToImmutable());
    }
    private static void Require(bool value,string message) { if (!value) throw new InvalidDataException(message); }
}
