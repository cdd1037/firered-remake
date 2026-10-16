using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SurfHeldItems(SpeciesId Species, string Common, string Rare);
public sealed record SurfMachineRow(SpeciesId Species, ImmutableArray<string> MachineSymbols);
public sealed record SurfSpeciesRates(SpeciesId Species, int CatchRate, int FleeRate);
public sealed record SurfSpeciesEvolutions(SpeciesId Species, ImmutableArray<CanonicalSourceEvolution> SourceEvolutions);
public sealed record SurfSpeciesCry(SpeciesId Species, int SourceSpeciesId, string CueId, int NormalTableIndex, string SourceSymbol,
    string SourceSamplePath, PokemonSourceRef NormalSource, PokemonSourceRef ReverseSource,
    PokemonSourceRef SampleBindingSource);
public sealed record SurfPokemonDocument(int SchemaVersion, ImmutableArray<PokemonSpeciesDefinition> Species, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<ForestPokemonConstant> Constants, ImmutableArray<PewterPokemonEncodedName> EncodedNames,
    ImmutableArray<MoveId> RequiredMoves, int? NaturalLevelLimit, ImmutableArray<SurfHeldItems> HeldItems,
    ImmutableArray<SurfMachineRow> Machines, ImmutableArray<SurfSpeciesRates> SurfRates,
    ImmutableArray<SurfSpeciesEvolutions> SourceEvolutions, ImmutableArray<SurfSpeciesCry> Cries);

/// <summary>Authenticated two-species additive metadata; preserves source evolution triples without executing them.</summary>
public sealed class PretSurfPokemon
{
    public const string Profile = "firered-surf-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string CatalogRelativePath = "content/generated/data/pokemon/surf.json";
    public PokemonCatalog Catalog { get; }
    public SurfPokemonDocument Document { get; }
    public ImmutableSortedDictionary<int, string> AdditionalSpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> AdditionalMoveNames { get; } = ImmutableDictionary<MoveId,string>.Empty;
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }

    private PretSurfPokemon(PokemonCatalog catalog,SurfPokemonDocument document)
    {
        Catalog = catalog; Document = document;
        AdditionalSpeciesNames = document.SpeciesNames.ToImmutableSortedDictionary(n => catalog.Species[n.Id].SourceSpeciesId, n => n.Name);
        Constants = document.Constants.ToImmutableDictionary(c => c.Symbol, c => c.Value, StringComparer.Ordinal);
        EncodedNames = document.EncodedNames.ToImmutableDictionary(n => n.Symbol, StringComparer.Ordinal);
    }

    /// <summary>Resolve regional presentation against the current admitted catalog.</summary>
    public static PretSurfPokemon Load(PretCanonicalPokemonCatalog canonical, PokemonCatalog catalog, string projectRoot)
    {
        var doc = SafariSource.Parse<SurfPokemonDocument>(SafariSource.Read(projectRoot, CatalogRelativePath), canonical: canonical);
        SafariSource.Require(doc.SchemaVersion == 1 && !doc.Species.IsDefaultOrEmpty &&
            doc.Species.All(species => catalog.Species.ContainsKey(species.Id)) &&
            doc.RequiredMoves.All(catalog.Moves.ContainsKey), "Surf presentation references are not admitted.");
        foreach (var name in doc.EncodedNames)
            SafariSource.Require(!name.Bytes.IsDefaultOrEmpty && name.Bytes[^1] == 255,
                "Surf encoded name is unterminated.");
        return new(catalog, doc);
    }
}
