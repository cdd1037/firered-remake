using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Exact Psyduck/Golduck ownership closure over an authenticated Safari110 root.
/// It enables ordinary creation/learning/evolution, never campaign or battle admission by itself.</summary>
public sealed class SurfOwnedPokemonCatalog
{
    public const string Profile = "firered-surf-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_PSYDUCK"), new("pret:SPECIES_GOLDUCK")];
    private static readonly ConditionalWeakTable<PokemonCatalog, SurfOwnedPokemonCatalog> Authenticated = new();
    public SafariOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    private SurfOwnedPokemonCatalog(SafariOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static bool IsPredecessor(PokemonCatalog? catalog) => SafariOwnedPokemonCatalog.IsAuthenticated(catalog);
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static SurfOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Authenticated.TryGetValue(catalog, out var proof) ? proof : null;

    public static SurfOwnedPokemonCatalog Authenticate(SafariOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!IsPredecessor(predecessor.Catalog) || predecessor.Catalog.Species.Count != 110 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 112 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var current) || !ReferenceEquals(p.Value, current)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("surf_owned_scope");
        foreach (var id in AdditionalSpecies)
        {
            var expected = predecessor.Mechanics.Catalog.Species[id];
            var raw = predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions;
            ImmutableArray<PokemonEvolution> evolutions = [];
            if (id == AdditionalSpecies[0])
            {
                if (raw.Length != 1 || raw[0] is not { Method: "EVO_LEVEL", SourceMethodId: 4,
                    Parameter: "33", ParameterValue: 33, Target.Value: "pret:SPECIES_GOLDUCK" })
                    throw new ArgumentException("surf_owned_evolution_source");
                evolutions = [new(33, raw[0].Target, raw[0].Source)];
            }
            else if (!raw.IsEmpty) throw new ArgumentException("surf_owned_golduck_evolution");
            expected = expected with { Evolutions = evolutions };
            var actual = candidate.Species[id];
            if (actual is null || actual.LevelUpMoves.IsDefault || actual.Evolutions.IsDefault ||
                !actual.LevelUpMoves.SequenceEqual(expected.LevelUpMoves) || !actual.Evolutions.SequenceEqual(evolutions) ||
                actual with { LevelUpMoves = expected.LevelUpMoves, Evolutions = expected.Evolutions } != expected)
                throw new ArgumentException("surf_owned_species_source");
        }
        var proof = new SurfOwnedPokemonCatalog(predecessor, candidate with { });
        Authenticated.Add(proof.Catalog, proof); return proof;
    }
}
