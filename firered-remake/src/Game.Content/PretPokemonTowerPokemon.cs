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
    }

    /// <summary>
    /// Overlay an authenticated 99-species predecessor. Pass the campaign catalog to retain its
    /// Shock Wave supplement (231 → 236 moves); the standalone Rocket catalog composes 230 → 235.
    /// </summary>
    public static PretPokemonTowerPokemon Load(PretCanonicalPokemonCatalog canonical, PokemonCatalog catalog, string projectRoot)
    {
        var doc = SafariSource.Parse<PokemonTowerPokemonDocument>(SafariSource.Read(projectRoot, CatalogRelativePath), canonical: canonical);
        SafariSource.Require(doc.SchemaVersion == 1 && !doc.Species.IsDefaultOrEmpty &&
            doc.Species.All(species => catalog.Species.ContainsKey(species.Id)) &&
            doc.RequiredMoves.All(catalog.Moves.ContainsKey), "PokemonTower presentation references are not admitted.");
        foreach (var name in doc.EncodedNames)
            SafariSource.Require(!name.Bytes.IsDefaultOrEmpty && name.Bytes[^1] == 255,
                "PokemonTower encoded name is unterminated.");
        var files = doc.Inputs.Select(input => new PretWorldSourceFile(input.Path)).ToImmutableArray();
        return new(catalog, doc, files);
    }

    /// <summary>Convenience loader for the standalone 230-move Rocket catalog.</summary>
    private static void Require(bool condition, string error)
    {
        if (!condition) throw new InvalidDataException(error);
    }
}
