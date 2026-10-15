using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SurfHeldItems(SpeciesId Species, string Common, string Rare, PokemonSourceRef Source);
public sealed record SurfMachineRow(SpeciesId Species, ImmutableArray<string> MachineSymbols, PokemonSourceRef Source);
public sealed record SurfSpeciesRates(SpeciesId Species, int CatchRate, int FleeRate, PokemonSourceRef Source);
public sealed record SurfSpeciesEvolutions(SpeciesId Species, ImmutableArray<CanonicalSourceEvolution> SourceEvolutions);
public sealed record SurfSpeciesCry(SpeciesId Species, int SourceSpeciesId, string CueId, int NormalTableIndex, string SourceSymbol,
    string SourceSamplePath, PokemonSourceRef NormalSource, PokemonSourceRef ReverseSource,
    PokemonSourceRef SampleBindingSource);
public sealed record SurfPokemonDocument(int SchemaVersion, string Profile, string SourceCommit, ImmutableArray<ForestPokemonInput> Inputs,
    ImmutableArray<PokemonSpeciesDefinition> Species, ImmutableArray<PretRoute1SpeciesName> SpeciesNames,
    ImmutableArray<ForestPokemonConstant> Constants, ImmutableArray<PewterPokemonEncodedName> EncodedNames,
    ImmutableArray<MoveId> RequiredMoves, int? NaturalLevelLimit, ImmutableArray<SurfHeldItems> HeldItems,
    ImmutableArray<SurfMachineRow> Machines, ImmutableArray<SurfSpeciesRates> SurfRates,
    ImmutableArray<SurfSpeciesEvolutions> SourceEvolutions, ImmutableArray<SurfSpeciesCry> Cries,
    ImmutableArray<string> Limitations);

/// <summary>Authenticated two-species additive metadata; preserves source evolution triples without executing them.</summary>
public sealed class PretSurfPokemon
{
    public const string Profile = "firered-surf-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string CatalogRelativePath = "content/generated/surf-pokemon/catalog.json";
    public PokemonCatalog Catalog { get; }
    public SurfPokemonDocument Document { get; }
    public ImmutableSortedDictionary<int, string> AdditionalSpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> AdditionalMoveNames { get; } = ImmutableDictionary<MoveId,string>.Empty;
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }

    private PretSurfPokemon(PokemonCatalog catalog,SurfPokemonDocument document,        ImmutableArray<PretWorldSourceFile> files)
    {
        Catalog = catalog; Document = document; SourceFiles = files;
        AdditionalSpeciesNames = document.SpeciesNames.ToImmutableSortedDictionary(n => catalog.Species[n.Id].SourceSpeciesId, n => n.Name);
        Constants = document.Constants.ToImmutableDictionary(c => c.Symbol, c => c.Value, StringComparer.Ordinal);
        EncodedNames = document.EncodedNames.ToImmutableDictionary(n => n.Symbol, StringComparer.Ordinal);
    }

    /// <summary>Strict authenticated110/354 predecessor; old records retain identity. Domain admission remains the caller's separate policy.</summary>
    public static PretSurfPokemon Load(PokemonCatalog baseCatalog, string projectRoot, string? dependencyProjectRoot = null)
    {
        PokemonFactory.ValidateCatalog(baseCatalog);
        SafariSource.Require(baseCatalog.Species.Count == 110 && baseCatalog.Moves.Count == 354,
            "Surf requires the authenticated110-species,354-move Safari predecessor.");
        string root = Path.GetFullPath(projectRoot), dependency = Path.GetFullPath(dependencyProjectRoot ?? root);
        var bytes = SafariSource.Read(root,CatalogRelativePath);
        var doc = SafariSource.Parse<SurfPokemonDocument>(bytes);
        SafariSource.Require(doc.SchemaVersion == 1 && doc.Species.Length == 2 && doc.SpeciesNames.Length == 2 &&
            doc.EncodedNames.Length == 2 && doc.RequiredMoves.Length == 9 && doc.HeldItems.Length == 2 &&
            doc.Machines.Length == 2 && doc.SurfRates.Length == 2 && doc.SourceEvolutions.Length == 2 &&
            doc.Cries.Length == 2 && doc.NaturalLevelLimit is null && !doc.Limitations.IsDefaultOrEmpty , "Surf source document scope differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        foreach (var input in doc.Inputs)
        {
            files.Add(new(input.Path,                $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{input.Path}"));
        }
        SafariSource.Require(doc.Species.Select(s => s.SourceSpeciesId).SequenceEqual([54,55]), "Surf exact species delta differs.");
        var species = baseCatalog.Species.ToBuilder();
        foreach (var s in doc.Species)
        {
            SafariSource.Require(s.Evolutions.IsEmpty, "Deferred Surf evolutions cannot be executed by this metadata projection.");
            species.Add(s.Id, s);
            var h = doc.HeldItems.Single(h => h.Species == s.Id);
            var rate = doc.SurfRates.Single(r => r.Species == s.Id);
            SafariSource.Require(rate.CatchRate == s.CatchRate ,
                "Surf source catch/escape rates differ.");         }
        SafariSource.Require(doc.RequiredMoves.ToHashSet().SetEquals(doc.Species.SelectMany(s => s.LevelUpMoves).Select(m => m.Move)) &&
            doc.RequiredMoves.All(baseCatalog.Moves.ContainsKey), "Surf full natural move closure differs.");
        foreach (var n in doc.EncodedNames)
        {
            SafariSource.Require(n.Bytes.Length == n.Name.Length + 1 && n.Bytes[^1] == 255, "Surf encoded name differs.");
        }
        foreach (var cry in doc.Cries)
        {
            SafariSource.Require(cry.SourceSpeciesId == species[cry.Species].SourceSpeciesId && cry.NormalTableIndex == cry.SourceSpeciesId - 1 , "Surf cry binding differs.");
        }
        // Canonical mechanics deliberately stores raw evolutions separately. Admit only this exact
        // ordinary source level33 edge; keep all other inherited ownership definitions unchanged.
        var psyduck = new SpeciesId("pret:SPECIES_PSYDUCK");
        var edge = doc.SourceEvolutions.Single(e => e.Species == psyduck).SourceEvolutions.Single();
        SafariSource.Require(edge.Method == "EVO_LEVEL" && edge.SourceMethodId == 4 && edge.Parameter == "33" &&
            edge.ParameterValue == 33 && edge.Target.Value == "pret:SPECIES_GOLDUCK", "Psyduck source evolution differs.");
        species[psyduck] = species[psyduck] with { Evolutions = [new(33, edge.Target, edge.Source)] };
        var result = new PokemonCatalog(SourceCommit, species.ToImmutable(), baseCatalog.Moves);
        SafariSource.Require(result.Species.Count == 112 && result.Moves.Count == 354 ,
            "Surf inherited metadata changed.");
        return new(result,doc,files.ToImmutable());
    }
}

