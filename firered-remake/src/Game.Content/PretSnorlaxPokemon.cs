using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SnorlaxPokemonDocument(int SchemaVersion, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<PretRoute1MoveName> MoveNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions,
    ImmutableArray<MoveId> RequiredMoves, int? NaturalLevelLimit);

/// <summary>Authenticated source143 and four absent moves. All fourteen learn entries remain metadata.</summary>
public sealed class PretSnorlaxPokemon
{
    public const string Profile = "firered-snorlax-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string CatalogRelativePath = "content/generated/data/pokemon/snorlax.json";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public SnorlaxPokemonDocument Document { get; }
    public ImmutableSortedDictionary<int, string> AdditionalSpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> AdditionalMoveNames { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<MtMoonDeferredEvolution> DeferredEvolutions => Document.DeferredEvolutions;

    private PretSnorlaxPokemon(PokemonCatalog catalog, SnorlaxPokemonDocument document)
    {
        Catalog = catalog; Document = document;
        AdditionalSpeciesNames = document.SpeciesNames.ToImmutableSortedDictionary(n => catalog.Species[n.Id].SourceSpeciesId, n => n.Name);
        AdditionalMoveNames = document.MoveNames.ToImmutableDictionary(n => n.Id, n => n.Name);
        Constants = document.Constants.ToImmutableDictionary(c => c.Symbol, c => c.Value, StringComparer.Ordinal);
        EncodedNames = document.EncodedNames.ToImmutableDictionary(n => n.Symbol, StringComparer.Ordinal);
    }

    /// <summary>Resolve regional presentation against the current admitted catalog.</summary>
    public static PretSnorlaxPokemon Load(PretCanonicalPokemonCatalog canonical, PokemonCatalog catalog, string projectRoot)
    {
        var doc = SafariSource.Parse<SnorlaxPokemonDocument>(SafariSource.Read(projectRoot, CatalogRelativePath), canonical: canonical);
        SafariSource.Require(doc.SchemaVersion == 1 && !doc.Species.IsDefaultOrEmpty &&
            doc.Species.All(species => catalog.Species.ContainsKey(species.Id)) &&
            doc.RequiredMoves.All(catalog.Moves.ContainsKey), "Snorlax presentation references are not admitted.");
        foreach (var name in doc.EncodedNames)
            SafariSource.Require(!name.Bytes.IsDefaultOrEmpty && name.Bytes[^1] == 255,
                "Snorlax encoded name is unterminated.");
        return new(catalog, doc);
    }
    private static void Require(bool value,string message) { if (!value) throw new InvalidDataException(message); }
}
