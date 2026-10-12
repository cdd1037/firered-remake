using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Exact Krabby/Kingler ownership extension over authenticated Silph113.</summary>
public sealed class CinnabarOwnedPokemonCatalog
{
    public const string Profile = "firered-cinnabar-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_KRABBY"), new("pret:SPECIES_KINGLER")];
    private static readonly ConditionalWeakTable<PokemonCatalog, CinnabarOwnedPokemonCatalog> Proofs = new();
    public SilphOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    private CinnabarOwnedPokemonCatalog(SilphOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static CinnabarOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static CinnabarOwnedPokemonCatalog Authenticate(SilphOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(SilphOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 115 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var actual) || !ReferenceEquals(p.Value, actual)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("cinnabar_owned_scope");
        foreach (var species in AdditionalSpecies)
        {
            var expected = predecessor.Mechanics.Catalog.Species[species];
            var raw = predecessor.Mechanics.SpeciesMechanics[species].SourceEvolutions;
            ImmutableArray<PokemonEvolution> evolutions = [];
            if (species == AdditionalSpecies[0])
            {
                if (raw.Length != 1 || raw[0] is not { Method: "EVO_LEVEL", SourceMethodId: 4,
                    Parameter: "28", ParameterValue: 28, Target.Value: "pret:SPECIES_KINGLER" })
                    throw new ArgumentException("cinnabar_owned_evolution_source");
                evolutions = [new(28,raw[0].Target,raw[0].Source)];
            }
            else if (!raw.IsEmpty) throw new ArgumentException("cinnabar_owned_kingler_evolution");
            if (!SilphOwnedPokemonCatalog.MatchesSource(candidate.Species[species], expected with { Evolutions = evolutions }))
                throw new ArgumentException("cinnabar_owned_source");
        }
        var proof = new CinnabarOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof); return proof;
    }
}
