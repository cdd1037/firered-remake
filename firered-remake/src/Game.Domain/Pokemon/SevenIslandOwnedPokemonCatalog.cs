using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Seven source-owned species and three natural evolution edges over the current Cave proof.</summary>
public sealed class SevenIslandOwnedPokemonCatalog
{
    public const string Profile = "firered-seven-island-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_PHANPY"), new("pret:SPECIES_DONPHAN"), new("pret:SPECIES_LARVITAR"), new("pret:SPECIES_PUPITAR"), new("pret:SPECIES_TYRANITAR"), new("pret:SPECIES_SKARMORY"), new("pret:SPECIES_UNOWN")];
    private static readonly ConditionalWeakTable<PokemonCatalog, SevenIslandOwnedPokemonCatalog> Proofs = new();
    public CeruleanCaveOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public OwnedBattlePokemonCatalog SharedBattle => OwnedBattlePokemonCatalog.For(Catalog)!;
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    private SevenIslandOwnedPokemonCatalog(CeruleanCaveOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static SevenIslandOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SevenIslandOwnedPokemonCatalog Authenticate(CeruleanCaveOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(CeruleanCaveOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 139 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 146 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var value) || !ReferenceEquals(p.Value, value)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("seven_island_owned_scope");
        foreach (var id in AdditionalSpecies)
            if (!MatchesSource(candidate.Species[id], ExpectedSpecies(predecessor, id)))
                throw new ArgumentException("seven_island_owned_source");
        var proof = new SevenIslandOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public static PokemonSpeciesDefinition ExpectedSpecies(CeruleanCaveOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(CeruleanCaveOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || !AdditionalSpecies.Contains(id))
            throw new ArgumentException("seven_island_owned_species");
        var expected = predecessor.Mechanics.Catalog.Species[id];
        var raw = predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions;
        var edge = id.Value switch {
            "pret:SPECIES_PHANPY" => (25, AdditionalSpecies[1]),
            "pret:SPECIES_LARVITAR" => (30, AdditionalSpecies[3]),
            "pret:SPECIES_PUPITAR" => (55, AdditionalSpecies[4]), _ => (0, default(SpeciesId)) };
        ImmutableArray<PokemonEvolution> evolutions = [];
        if (edge.Item1 != 0)
        {
            if (raw.Length != 1 || raw[0].Method != "EVO_LEVEL" || raw[0].SourceMethodId != 4 ||
                raw[0].Parameter != edge.Item1.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                raw[0].ParameterValue != edge.Item1 || raw[0].Target != edge.Item2)
                throw new ArgumentException("seven_island_owned_evolution_source");
            evolutions = [new(edge.Item1, edge.Item2, raw[0].Source)];
        }
        else if (!raw.IsEmpty) throw new ArgumentException("seven_island_owned_terminal_evolution");
        return evolutions.IsEmpty ? expected : expected with { Evolutions = evolutions };
    }
    internal static bool MatchesSource(PokemonSpeciesDefinition? actual, PokemonSpeciesDefinition expected) =>
        actual is not null && !actual.LevelUpMoves.IsDefault && !actual.Evolutions.IsDefault &&
        actual.LevelUpMoves.SequenceEqual(expected.LevelUpMoves) && actual.Evolutions.SequenceEqual(expected.Evolutions) &&
        actual with { LevelUpMoves = expected.LevelUpMoves, Evolutions = expected.Evolutions } == expected;
}
