using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SnorlaxPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions,
    ImmutableArray<MoveId> RequiredMoves, int? NaturalLevelLimit, ImmutableArray<string> Limitations);

/// <summary>Authenticated source143 and four absent moves. All fourteen learn entries remain metadata.</summary>
public sealed class PretSnorlaxPokemon
{
    public const string Profile = "firered-snorlax-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string CatalogRelativePath = "content/generated/snorlax-pokemon/catalog.json";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public SnorlaxPokemonDocument Document { get; }
    public ImmutableSortedDictionary<int, string> AdditionalSpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> AdditionalMoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions => Document.DeferredEvolutions;
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }

    private PretSnorlaxPokemon(PokemonCatalog catalog, SnorlaxPokemonDocument document,
        ImmutableArray<PretWorldSourceFile> files)
    {
        Catalog = catalog; Document = document; SourceFiles = files;
        AdditionalSpeciesNames = document.SpeciesNames.ToImmutableSortedDictionary(n => catalog.Species[n.Id].SourceSpeciesId, n => n.Name);
        AdditionalMoveNames = document.MoveNames.ToImmutableDictionary(n => n.Id, n => n.Name);
        Constants = document.Constants.ToImmutableDictionary(c => c.Symbol, c => c.Value, StringComparer.Ordinal);
        EncodedNames = document.EncodedNames.ToImmutableDictionary(n => n.Symbol, StringComparer.Ordinal);
    }

    /// <summary>Composes 101/236 campaign or 101/235 standalone into102/240 or102/239; keeps inherited objects.</summary>
    public static PretSnorlaxPokemon Load(PokemonCatalog baseCatalog, string projectRoot, string? dependencyProjectRoot = null)
    {
        PokemonFactory.ValidateCatalog(baseCatalog);
        Require(baseCatalog.Species.Count == 101 &&
            baseCatalog.Moves.Count is 235 or 236, "Snorlax predecessor scope differs.");
        var project = Path.GetFullPath(projectRoot);
        // All new source bytes are self-contained; dependencyProjectRoot is retained for composition API consistency.
        var bytes = PretCeladonApproachSource.Read(project,CatalogRelativePath);
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        options.Converters.Add(new ExactEnumConverter<PokemonEvolutionMethod>());
        var doc = ContentJson.ParseStrict<SnorlaxPokemonDocument>(Utf8.GetString(bytes), options);
        Require(doc.SchemaVersion == 1 && doc.Species.Length == 1 && doc.Moves.Length == 4 &&
            doc.SpeciesNames.Length == 1 && doc.MoveNames.Length == 4 && doc.EncodedNames.Length == 5 &&
            doc.Constants.Length == 18 && doc.DeferredEvolutions.IsEmpty && doc.RequiredMoves.Length == 14 &&
            doc.NaturalLevelLimit is null && !doc.Limitations.IsDefaultOrEmpty, "Snorlax document scope differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var input in doc.Inputs)
        {
            files.Add(new(input.Path,                $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{input.Path}"));
        }
        var s = doc.Species.Single();
        Require(s.Id.Value == "pret:SPECIES_SNORLAX" && s.SourceSpeciesId == 143 && s.LevelUpMoves.Length == 14 &&
            s.Type1 == "TYPE_NORMAL" && s.Type2 == "TYPE_NORMAL" && s.Ability1 == "ABILITY_IMMUNITY" && s.Ability2 == "ABILITY_THICK_FAT" &&
            s.GenderRatio == 31 && s.GrowthRate == GrowthRate.Slow && s.Friendship == 70 && s.CatchRate == 25 && s.ExperienceYield == 154 &&
            s.BaseStats == new SixStats(160,110,65,30,65,110) && s.EvYield == new SixStats(2,0,0,0,0,0) && s.Evolutions.IsEmpty,
            "Snorlax source species identity differs.");
        var expected = new[] { (1,"TACKLE"),(5,"AMNESIA"),(9,"DEFENSE_CURL"),(13,"BELLY_DRUM"),(17,"HEADBUTT"),(21,"YAWN"),(25,"REST"),(29,"SNORE"),(33,"BODY_SLAM"),(37,"SLEEP_TALK"),(41,"BLOCK"),(45,"COVET"),(49,"ROLLOUT"),(53,"HYPER_BEAM") };
        Require(s.LevelUpMoves.Select(e => (e.Level,e.Move.Value)).SequenceEqual(expected.Select(e => (e.Item1,"pret:MOVE_"+e.Item2))),
            "Snorlax complete learnset differs.");
        var species = baseCatalog.Species.Add(s.Id,s); var moves = baseCatalog.Moves.ToBuilder();
        Require(doc.Moves.Select(m => m.SourceMoveId).ToHashSet().SetEquals(new[] {173,214,335,343}), "Snorlax new move identities differ.");
        foreach (var m in doc.Moves) { Require(!m.BattleEffectImplemented, "Metadata cannot admit effects."); moves.Add(m.Id,m); }
        Require(doc.RequiredMoves.Distinct().Count() == 14 && doc.RequiredMoves.All(moves.ContainsKey), "Snorlax move closure differs.");
        foreach (var n in doc.EncodedNames)
        {
            Require(n.Bytes.Length == n.Name.Length + 1 && n.Bytes[^1] == 255 && n.Bytes.Take(n.Bytes.Length-1).All(b => b is >=0 and <255),
                "Snorlax encoded name differs.");
        }
        var catalog = new PokemonCatalog(SourceCommit,species,moves.ToImmutable());
        Require(catalog.Species.Count == 102 && catalog.Moves.Count == baseCatalog.Moves.Count + 4 , "Snorlax inherited records differ.");
        // The battle/domain increment owns the exact102-species factory admission.
        PokemonFactory.ValidateCatalog(catalog);
        return new(catalog,doc,files.ToImmutable());
    }
    private static void Require(bool value,string message) { if (!value) throw new InvalidDataException(message); }
}
