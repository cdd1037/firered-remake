using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Exact Lapras ownership addition over the authenticated Surf112 catalog.</summary>
public sealed class SilphOwnedPokemonCatalog
{
    public const string Profile = "firered-silph-owned-pokemon-v1";
    public static SpeciesId Lapras { get; } = new("pret:SPECIES_LAPRAS");
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } = [Lapras];
    private static readonly ConditionalWeakTable<PokemonCatalog, SilphOwnedPokemonCatalog> Proofs = new();
    public SurfOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    private SilphOwnedPokemonCatalog(SurfOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static SilphOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SilphOwnedPokemonCatalog Authenticate(SurfOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(SurfOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 113 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var actual) || !ReferenceEquals(p.Value, actual)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).SequenceEqual(AdditionalSpecies))
            throw new ArgumentException("silph_owned_scope");
        if (!MatchesSource(candidate.Species[Lapras], predecessor.Mechanics.Catalog.Species[Lapras]) ||
            !predecessor.Mechanics.SpeciesMechanics[Lapras].SourceEvolutions.IsEmpty)
            throw new ArgumentException("silph_owned_lapras_source");
        var proof = new SilphOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    internal static bool MatchesSource(PokemonSpeciesDefinition? actual, PokemonSpeciesDefinition expected) =>
        actual is not null && !actual.LevelUpMoves.IsDefault && !actual.Evolutions.IsDefault &&
        actual.LevelUpMoves.SequenceEqual(expected.LevelUpMoves) && actual.Evolutions.SequenceEqual(expected.Evolutions) &&
        actual with { LevelUpMoves = expected.LevelUpMoves, Evolutions = expected.Evolutions } == expected;
}
