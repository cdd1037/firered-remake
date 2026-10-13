using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Six original wild/level species plus Bellossom, preserving every predecessor species and all shared mechanics.</summary>
public sealed class SixIslandOwnedPokemonCatalog
{
    public const string Profile = "firered-six-island-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_SENTRET"), new("pret:SPECIES_FURRET"), new("pret:SPECIES_NATU"),
         new("pret:SPECIES_XATU"), new("pret:SPECIES_YANMA"), new("pret:SPECIES_WOBBUFFET"), new("pret:SPECIES_BELLOSSOM")];
    private static readonly ConditionalWeakTable<PokemonCatalog, SixIslandOwnedPokemonCatalog> Proofs = new();
    public FourIslandOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    private SixIslandOwnedPokemonCatalog(FourIslandOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static SixIslandOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SixIslandOwnedPokemonCatalog Authenticate(FourIslandOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(FourIslandOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 125 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 132 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var value) || !ReferenceEquals(p.Value, value)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("six_island_owned_scope");
        foreach (var id in AdditionalSpecies)
            if (!MatchesSource(candidate.Species[id], ExpectedSpecies(predecessor, id)))
                throw new ArgumentException("six_island_owned_source");
        var proof = new SixIslandOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public static PokemonSpeciesDefinition ExpectedSpecies(FourIslandOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(FourIslandOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || !AdditionalSpecies.Contains(id))
            throw new ArgumentException("six_island_owned_species");
        var expected = predecessor.Mechanics.Catalog.Species[id];
        var raw = predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions;
        var edge = id.Value switch {
            "pret:SPECIES_SENTRET" => (15, AdditionalSpecies[1]),
            "pret:SPECIES_NATU" => (25, AdditionalSpecies[3]), _ => (0, default(SpeciesId)) };
        ImmutableArray<PokemonEvolution> evolutions = [];
        if (edge.Item1 != 0)
        {
            if (raw.Length != 1 || raw[0].Method != "EVO_LEVEL" || raw[0].SourceMethodId != 4 ||
                raw[0].Parameter != edge.Item1.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                raw[0].ParameterValue != edge.Item1 || raw[0].Target != edge.Item2)
                throw new ArgumentException("six_island_owned_evolution_source");
            evolutions = [new(edge.Item1, edge.Item2, raw[0].Source)];
        }
        else if (!raw.IsEmpty) throw new ArgumentException("six_island_owned_terminal_evolution");
        return expected with { Evolutions = evolutions };
    }
    internal static bool MatchesSource(PokemonSpeciesDefinition? actual, PokemonSpeciesDefinition expected) =>
        actual is not null && !actual.LevelUpMoves.IsDefault && !actual.Evolutions.IsDefault &&
        actual.LevelUpMoves.SequenceEqual(expected.LevelUpMoves) && actual.Evolutions.SequenceEqual(expected.Evolutions) &&
        actual with { LevelUpMoves = expected.LevelUpMoves, Evolutions = expected.Evolutions } == expected;
}
