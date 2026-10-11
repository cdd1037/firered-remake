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
public sealed record SurfSpeciesCry(SpeciesId Species, int SourceSpeciesId, string CueId, int NormalTableIndex,
    string SourceSymbol, string SourceSamplePath, string SourceSampleSha256, PokemonSourceRef NormalSource,
    PokemonSourceRef ReverseSource, PokemonSourceRef SampleBindingSource);
public sealed record SurfPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<ForestPokemonInput> Inputs, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PretRoute1SpeciesName> SpeciesNames, ImmutableArray<ForestPokemonConstant> Constants,
    ImmutableArray<PewterPokemonEncodedName> EncodedNames, ImmutableArray<MoveId> RequiredMoves,
    int? NaturalLevelLimit, ImmutableArray<SurfHeldItems> HeldItems, ImmutableArray<SurfMachineRow> Machines,
    ImmutableArray<SurfSpeciesRates> SurfRates, ImmutableArray<SurfSpeciesEvolutions> SourceEvolutions,
    ImmutableArray<SurfSpeciesCry> Cries, string CanonicalCatalogSha256, ImmutableArray<string> Limitations);

/// <summary>Authenticated two-species additive metadata; preserves source evolution triples without executing them.</summary>
public sealed class PretSurfPokemon
{
    public const string Profile = "firered-surf-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string LockSha256 = "37b7b55818bb9e6dd137a36293f3e6a1657366bfe7c3e2cddbc17a5c36a0ef95";
    public const int LockByteLength = 11206;
    public const int InputCount = 46;
    public const string CatalogSha256 = "cd93555c2d907397793d99f7ddb057e458ee474c9eff852412eddf10b866f8f9";
    public const int CatalogByteLength = 25958;
    public const string CatalogRelativePath = "content/generated/surf-pokemon/catalog.json";
    public const string FixturesSha256 = "ecd19573a2f6795690ee6cd373e6127d260a2f25d9b50fff9932b0da2f9121ba";
    public PokemonCatalog Catalog { get; }
    public SurfPokemonDocument Document { get; }
    public ImmutableSortedDictionary<int, string> AdditionalSpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> AdditionalMoveNames { get; } = ImmutableDictionary<MoveId,string>.Empty;
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }

    private PretSurfPokemon(PokemonCatalog catalog, SurfPokemonDocument document,
        ImmutableArray<PretWorldSourceFile> files, PretCanonicalPokemonCatalog canonical)
    {
        Catalog = catalog; Document = document; SourceFiles = files;
        AdditionalSpeciesNames = document.SpeciesNames.ToImmutableSortedDictionary(n => catalog.Species[n.Id].SourceSpeciesId, n => n.Name);
        Constants = document.Constants.ToImmutableDictionary(c => c.Symbol, c => c.Value, StringComparer.Ordinal);
        EncodedNames = document.EncodedNames.ToImmutableDictionary(n => n.Symbol, StringComparer.Ordinal);
        AdditionalSourceHashes = ["surf-pokemon-lock:" + LockSha256, "surf-pokemon-catalog:" + CatalogSha256,
            .. files.Select(f => "surf-pokemon:" + f.Path + ":" + f.Sha256), .. canonical.SourceHashes];
    }

    /// <summary>Strict authenticated110/354 predecessor; old records retain identity. Domain admission remains the caller's separate policy.</summary>
    public static PretSurfPokemon Load(PokemonCatalog baseCatalog, string projectRoot, string? dependencyProjectRoot = null)
    {
        PokemonFactory.ValidateCatalog(baseCatalog);
        SafariSource.Require(baseCatalog.Species.Count == 110 && SurfOwnedPokemonCatalog.IsPredecessor(baseCatalog) && baseCatalog.Moves.Count == 354,
            "Surf requires the authenticated110-species,354-move Safari predecessor.");
        string root = Path.GetFullPath(projectRoot), dependency = Path.GetFullPath(dependencyProjectRoot ?? root);
        var bytes = SafariSource.Read(root, CatalogRelativePath, CatalogSha256, CatalogByteLength);
        _ = SafariSource.Read(root, "scripts/surf-pokemon.lock.json", LockSha256, LockByteLength);
        var doc = SafariSource.Parse<SurfPokemonDocument>(bytes);
        SafariSource.Require(doc.SchemaVersion == 1 && doc.Profile == Profile && doc.SourceCommit == SourceCommit &&
            doc.Inputs.Length == InputCount && doc.Species.Length == 2 && doc.SpeciesNames.Length == 2 &&
            doc.EncodedNames.Length == 2 && doc.RequiredMoves.Length == 9 && doc.HeldItems.Length == 2 &&
            doc.Machines.Length == 2 && doc.SurfRates.Length == 2 && doc.SourceEvolutions.Length == 2 &&
            doc.Cries.Length == 2 && doc.NaturalLevelLimit is null && !doc.Limitations.IsDefaultOrEmpty &&
            doc.CanonicalCatalogSha256 == PretCanonicalPokemonCatalog.CatalogSha256, "Surf source document scope differs.");
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var lineCounts = new Dictionary<string,int>(StringComparer.Ordinal);
        foreach (var input in doc.Inputs)
        {
            var source = SafariSource.Read(root, "content/raw/surf-pokemon/" + input.Path, input.Sha256, input.ByteLength);
            SafariSource.Require(SafariSource.GitBlob(source) == input.GitBlobSha1, "Surf Git blob differs: " + input.Path);
            if (!input.Path.EndsWith(".png", StringComparison.Ordinal) && !input.Path.EndsWith(".wav", StringComparison.Ordinal))
                lineCounts.Add(input.Path, new UTF8Encoding(false,true).GetString(source).Count(c => c == '\n') + 1);
            files.Add(new(input.Path, input.GitBlobSha1, input.ByteLength, input.Sha256,
                $"https://raw.githubusercontent.com/pret/pokefirered/{SourceCommit}/{input.Path}"));
        }
        void Source(PokemonSourceRef r) => SafariSource.Require(doc.Inputs.Any(f => f.Path == r.Path && f.Sha256 == r.Sha256) &&
            r.StartLine > 0 && r.EndLine >= r.StartLine && lineCounts.TryGetValue(r.Path, out int lines) &&
            r.EndLine <= lines && !string.IsNullOrWhiteSpace(r.Symbol), "Surf source reference differs.");
        var canonical = PretCanonicalPokemonCatalog.Load(dependency, dependency);
        var moveSource = PretMetronomeMoveCatalog.Load(dependency);
        SafariSource.Require(baseCatalog.Moves.Values.All(moveSource.MatchesSource), "Surf predecessor move source differs.");
        SafariSource.Require(doc.Species.Select(s => s.SourceSpeciesId).SequenceEqual([54,55]), "Surf exact species delta differs.");
        var species = baseCatalog.Species.ToBuilder();
        foreach (var s in doc.Species)
        {
            SafariSource.Require(JsonSerializer.Serialize(s) == JsonSerializer.Serialize(canonical.Catalog.Species[s.Id]),
                "Surf species differs from authenticated canonical386: " + s.Id.Value);
            Source(s.Source); Source(s.LearnsetSource);
            foreach (var row in s.LevelUpMoves) Source(row.Source);
            SafariSource.Require(s.Evolutions.IsEmpty, "Deferred Surf evolutions cannot be executed by this metadata projection.");
            species.Add(s.Id, s);
            var metadata = canonical.Metadata[s.Id];
            var originals = doc.SourceEvolutions.Single(e => e.Species == s.Id);
            SafariSource.Require(JsonSerializer.Serialize(originals.SourceEvolutions) == JsonSerializer.Serialize(metadata.SourceEvolutions),
                "Surf original evolution triples differ.");
            foreach (var e in originals.SourceEvolutions) Source(e.Source);
            var h = doc.HeldItems.Single(h => h.Species == s.Id);
            SafariSource.Require(h.Common == metadata.OriginalFields["itemCommon"] && h.Rare == metadata.OriginalFields["itemRare"],
                "Surf original held-item slots differ."); Source(h.Source);
            var rate = doc.SurfRates.Single(r => r.Species == s.Id);
            SafariSource.Require(rate.CatchRate == s.CatchRate && rate.FleeRate == int.Parse(metadata.OriginalFields["safariZoneFleeRate"]),
                "Surf source catch/escape rates differ."); Source(rate.Source);
        }
        SafariSource.Require(doc.RequiredMoves.ToHashSet().SetEquals(doc.Species.SelectMany(s => s.LevelUpMoves).Select(m => m.Move)) &&
            doc.RequiredMoves.All(baseCatalog.Moves.ContainsKey), "Surf full natural move closure differs.");
        foreach (var n in doc.SpeciesNames) Source(n.Source);
        foreach (var c in doc.Constants) Source(c.Source);
        foreach (var n in doc.EncodedNames)
        {
            Source(n.Source); Source(n.EncodingSource);
            SafariSource.Require(n.Bytes.Length == n.Name.Length + 1 && n.Bytes[^1] == 255, "Surf encoded name differs.");
        }
        foreach (var m in doc.Machines) Source(m.Source);
        foreach (var cry in doc.Cries)
        {
            Source(cry.NormalSource); Source(cry.ReverseSource); Source(cry.SampleBindingSource);
            SafariSource.Require(cry.SourceSpeciesId == species[cry.Species].SourceSpeciesId && cry.NormalTableIndex == cry.SourceSpeciesId - 1 &&
                doc.Inputs.Any(i => i.Path == cry.SourceSamplePath && i.Sha256 == cry.SourceSampleSha256), "Surf cry binding differs.");
        }
        // Canonical mechanics deliberately stores raw evolutions separately. Admit only this exact
        // ordinary source level33 edge; keep all other inherited ownership definitions unchanged.
        var psyduck = new SpeciesId("pret:SPECIES_PSYDUCK");
        var edge = doc.SourceEvolutions.Single(e => e.Species == psyduck).SourceEvolutions.Single();
        SafariSource.Require(edge.Method == "EVO_LEVEL" && edge.SourceMethodId == 4 && edge.Parameter == "33" &&
            edge.ParameterValue == 33 && edge.Target.Value == "pret:SPECIES_GOLDUCK", "Psyduck source evolution differs.");
        species[psyduck] = species[psyduck] with { Evolutions = [new(33, edge.Target, edge.Source)] };
        var result = new PokemonCatalog(SourceCommit, species.ToImmutable(), baseCatalog.Moves);
        SafariSource.Require(result.Species.Count == 112 && result.Moves.Count == 354 &&
            baseCatalog.Species.All(p => ReferenceEquals(p.Value, result.Species[p.Key])) && ReferenceEquals(result.Moves, baseCatalog.Moves),
            "Surf inherited metadata changed.");
        return new(result, doc, files.ToImmutable(), canonical);
    }
}

