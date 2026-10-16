using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SilphPokemonDocument(int SchemaVersion, ImmutableArray<PokemonSpeciesDefinition> Species,
    ImmutableArray<PokemonSpeciesDefinition> OpponentSpecies,
    ImmutableArray<PretRoute1SpeciesName> SpeciesNames, ImmutableArray<PewterPokemonEncodedName> EncodedNames,
    ImmutableArray<SafariMachineRow> Machines, ImmutableArray<SafariHeldItems> HeldItems,
    ImmutableArray<SafariSpeciesEvolutions> SourceEvolutions, ImmutableArray<MoveId> RequiredMoves);

/// <summary>Exact Lapras ownership metadata; Alakazam/Mr. Mime remain explicitly opponent-only.</summary>
public sealed class PretSilphPokemon
{
    public const string Profile = "firered-silph-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public PokemonCatalog Catalog { get; }
    public SilphPokemonDocument Document { get; }
    public ImmutableArray<PokemonSpeciesDefinition> OpponentSpecies => Document.OpponentSpecies;
    public ImmutableSortedDictionary<int, string> AdditionalSpeciesNames { get; }
    public ImmutableDictionary<MoveId, string> AdditionalMoveNames { get; } = ImmutableDictionary<MoveId, string>.Empty;
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, PewterPokemonEncodedName> EncodedNames { get; }
    private PretSilphPokemon(PokemonCatalog catalog, SilphPokemonDocument doc, SilphSourcePackage source)
    {
        Catalog = catalog; Document = doc;
        AdditionalSpeciesNames = doc.SpeciesNames.ToImmutableSortedDictionary(n => doc.Species.Concat(doc.OpponentSpecies).Single(s => s.Id == n.Id).SourceSpeciesId, n => n.Name);
        Constants = doc.Species.Concat(doc.OpponentSpecies).ToImmutableDictionary(s => s.Id.Value[5..], s => s.SourceSpeciesId, StringComparer.Ordinal);
        EncodedNames = doc.EncodedNames.ToImmutableDictionary(n => n.Symbol, StringComparer.Ordinal);
    }
    public static PretSilphPokemon Load(PretCanonicalPokemonCatalog canonical, PokemonCatalog catalog, string root)
    {
        var (document, source) = LoadSource(canonical, root, null);
        SilphSourcePackage.Need(document.Species.All(species => catalog.Species.ContainsKey(species.Id)),
            "Silph presentation references are not admitted.");
        return new(catalog, document, source);
    }
    public static SilphPokemonDocument LoadDocument(PretCanonicalPokemonCatalog canonical, string root, string? dependencyProjectRoot = null) => LoadSource(canonical, root, dependencyProjectRoot).Document;
    private static (SilphPokemonDocument Document, SilphSourcePackage Source) LoadSource(PretCanonicalPokemonCatalog canonical, string root, string? dependencyProjectRoot)
    {
        var source = SilphSourcePackage.Catalogs(root); var d = source.Parse<SilphPokemonDocument>("pokemon.json", canonical);
        SilphSourcePackage.Need(d.SchemaVersion == 1 &&
            d.Species.Select(s => s.SourceSpeciesId).SequenceEqual([131]) && d.OpponentSpecies.Select(s => s.SourceSpeciesId).SequenceEqual([65,122]) &&
            d.SpeciesNames.Length == 3 && d.EncodedNames.Length == 3 && d.Machines.Length == 3 && d.HeldItems.Length == 3 &&
            d.SourceEvolutions.Length == 3 ,
            "Silph species document scope differs.");
        foreach (var s in d.Species.Concat(d.OpponentSpecies))
        {
            var h = d.HeldItems.Single(h => h.Species == s.Id);         }
        SilphSourcePackage.Need(d.RequiredMoves.ToHashSet().SetEquals(d.Species.Concat(d.OpponentSpecies).SelectMany(s => s.LevelUpMoves).Select(m => m.Move)) , "Silph full natural move closure differs.");
        foreach (var n in d.EncodedNames) { SilphSourcePackage.Need(n.Bytes[^1] == 255, "Silph name terminator differs."); }
        return (d, source);
    }

}
