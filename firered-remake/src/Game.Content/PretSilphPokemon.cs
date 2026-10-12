using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SilphPokemonDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PokemonSpeciesDefinition> Species, ImmutableArray<PokemonSpeciesDefinition> OpponentSpecies,
    ImmutableArray<PretRoute1SpeciesName> SpeciesNames, ImmutableArray<PewterPokemonEncodedName> EncodedNames,
    ImmutableArray<SafariMachineRow> Machines, ImmutableArray<SafariHeldItems> HeldItems,
    ImmutableArray<SafariSpeciesEvolutions> SourceEvolutions, ImmutableArray<MoveId> RequiredMoves,
    string CanonicalCatalogSha256, ImmutableArray<string> Limitations);

/// <summary>Exact Lapras ownership metadata; Alakazam/Mr. Mime remain explicitly opponent-only.</summary>
public sealed class PretSilphPokemon
{
    public const string Profile = "firered-silph-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string CatalogSha256 = "b49c9c278259869d4b4208e128f22628350eb7ac4cc22b203c3ab80d95ca91ae";
    public PokemonCatalog Catalog { get; }
    public SilphPokemonDocument Document { get; }
    public ImmutableArray<PokemonSpeciesDefinition> OpponentSpecies => Document.OpponentSpecies;
    public ImmutableSortedDictionary<int, string> AdditionalSpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> AdditionalMoveNames { get; } = ImmutableDictionary<MoveId, string>.Empty;
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretSilphPokemon(PokemonCatalog catalog, SilphPokemonDocument doc, SilphSourcePackage source)
    {
        Catalog = catalog; Document = doc; SourceFiles = source.Files;
        AdditionalSpeciesNames = doc.SpeciesNames.ToImmutableSortedDictionary(n => doc.Species.Concat(doc.OpponentSpecies).Single(s => s.Id == n.Id).SourceSpeciesId, n => n.Name);
        Constants = doc.Species.Concat(doc.OpponentSpecies).ToImmutableDictionary(s => s.Id.Value[5..], s => s.SourceSpeciesId, StringComparer.Ordinal);
        EncodedNames = doc.EncodedNames.ToImmutableDictionary(n => n.Symbol, StringComparer.Ordinal);
        AdditionalSourceHashes = ["silph-pokemon:" + CatalogSha256, .. source.Files.Select(f => "silph-catalog-source:" + f.Path + ":" + f.Sha256)];
    }
    public static PretSilphPokemon Load(PokemonCatalog previous, string root, string? dependencyProjectRoot = null)
    {
        PokemonFactory.ValidateCatalog(previous);
        SilphSourcePackage.Need(SurfOwnedPokemonCatalog.IsAuthenticated(previous) && previous.Species.Count == 112 && previous.Moves.Count == 354,
            "Silph requires the authenticated112-species Surf predecessor.");
        var (d, source) = LoadSource(root, dependencyProjectRoot);
        var result = previous with { Species = previous.Species.Add(d.Species[0].Id, d.Species[0]) };
        SilphSourcePackage.Need(result.Species.Count == 113 && previous.Species.All(p => ReferenceEquals(p.Value, result.Species[p.Key])) &&
            ReferenceEquals(previous.Moves, result.Moves) && d.OpponentSpecies.All(s => !result.Species.ContainsKey(s.Id)), "Silph ownership boundary differs.");
        return new(result, d, source);
    }
    public static SilphPokemonDocument LoadDocument(string root, string? dependencyProjectRoot = null) => LoadSource(root, dependencyProjectRoot).Document;
    private static (SilphPokemonDocument Document, SilphSourcePackage Source) LoadSource(string root, string? dependencyProjectRoot)
    {
        var source = SilphSourcePackage.Catalogs(root); var d = source.Parse<SilphPokemonDocument>("pokemon.json");
        SilphSourcePackage.Need(d.SchemaVersion == 1 && d.Profile == Profile && d.SourceCommit == SourceCommit &&
            d.Species.Select(s => s.SourceSpeciesId).SequenceEqual([131]) && d.OpponentSpecies.Select(s => s.SourceSpeciesId).SequenceEqual([65,122]) &&
            d.SpeciesNames.Length == 3 && d.EncodedNames.Length == 3 && d.Machines.Length == 3 && d.HeldItems.Length == 3 &&
            d.SourceEvolutions.Length == 3 && !d.Limitations.IsDefaultOrEmpty && d.CanonicalCatalogSha256 == PretCanonicalPokemonCatalog.CatalogSha256,
            "Silph species document scope differs.");
        var canonical = PretCanonicalPokemonCatalog.Load(dependencyProjectRoot ?? root, dependencyProjectRoot ?? root);
        foreach (var s in d.Species.Concat(d.OpponentSpecies))
        {
            SilphSourcePackage.Need(JsonSerializer.Serialize(s) == JsonSerializer.Serialize(canonical.Catalog.Species[s.Id]), "Silph canonical species differs.");
            source.Reference(s.Source); source.Reference(s.LearnsetSource); foreach (var row in s.LevelUpMoves) source.Reference(row.Source);
            var meta = canonical.Metadata[s.Id]; var original = d.SourceEvolutions.Single(e => e.Species == s.Id);
            SilphSourcePackage.Need(JsonSerializer.Serialize(original.SourceEvolutions) == JsonSerializer.Serialize(meta.SourceEvolutions), "Silph source evolution differs.");
            foreach (var e in original.SourceEvolutions) source.Reference(e.Source);
            var h = d.HeldItems.Single(h => h.Species == s.Id); source.Reference(h.Source);
            SilphSourcePackage.Need(h.Common == meta.OriginalFields["itemCommon"] && h.Rare == meta.OriginalFields["itemRare"], "Silph held-item source differs.");
        }
        SilphSourcePackage.Need(d.RequiredMoves.ToHashSet().SetEquals(d.Species.Concat(d.OpponentSpecies).SelectMany(s => s.LevelUpMoves).Select(m => m.Move)) &&
            d.RequiredMoves.All(canonical.Catalog.Moves.ContainsKey), "Silph full natural move closure differs.");
        foreach (var n in d.SpeciesNames) source.Reference(n.Source);
        foreach (var n in d.EncodedNames) { source.Reference(n.Source); source.Reference(n.EncodingSource); SilphSourcePackage.Need(n.Bytes[^1] == 255, "Silph name terminator differs."); }
        foreach (var m in d.Machines) source.Reference(m.Source);
        return (d, source);
    }

}
