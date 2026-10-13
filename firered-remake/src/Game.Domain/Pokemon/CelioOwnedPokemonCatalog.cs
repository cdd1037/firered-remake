using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>The three original roaming beasts over Five Island; every inherited species/move retains identity.</summary>
public sealed class CelioOwnedPokemonCatalog
{
    public const string Profile = "firered-celio-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_ENTEI"), new("pret:SPECIES_RAIKOU"), new("pret:SPECIES_SUICUNE")];
    private static readonly ConditionalWeakTable<PokemonCatalog, CelioOwnedPokemonCatalog> Proofs = new();
    public FiveIslandOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    private CelioOwnedPokemonCatalog(FiveIslandOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static CelioOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static CelioOwnedPokemonCatalog Authenticate(FiveIslandOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(FiveIslandOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 135 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 138 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var value) || !ReferenceEquals(p.Value, value)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("celio_owned_scope");
        foreach (var id in AdditionalSpecies)
            if (!MatchesSource(candidate.Species[id], ExpectedSpecies(predecessor, id)))
                throw new ArgumentException("celio_owned_source");
        var proof = new CelioOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public static PokemonSpeciesDefinition ExpectedSpecies(FiveIslandOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(FiveIslandOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || !AdditionalSpecies.Contains(id))
            throw new ArgumentException("celio_owned_species");
        var expected = predecessor.Mechanics.Catalog.Species[id];
        if (!predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions.IsEmpty || !expected.Evolutions.IsEmpty)
            throw new ArgumentException("celio_owned_terminal_evolution");
        return expected;
    }
    internal static bool MatchesSource(PokemonSpeciesDefinition? actual, PokemonSpeciesDefinition expected) =>
        actual is not null && !actual.LevelUpMoves.IsDefault && !actual.Evolutions.IsDefault &&
        actual.LevelUpMoves.SequenceEqual(expected.LevelUpMoves) && actual.Evolutions.SequenceEqual(expected.Evolutions) &&
        actual with { LevelUpMoves = expected.LevelUpMoves, Evolutions = expected.Evolutions } == expected;
}
