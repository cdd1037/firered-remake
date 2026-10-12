using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Exact Saffron trainer continuation: the same 113 owned species, the two
/// inherited Silph opponents, and Slowbro. No additional permanent ownership is granted.</summary>
public sealed class SaffronGymOpponentPokemonCatalog
{
    public const string Profile = "firered-saffron-gym-opponents-v1";
    public static SpeciesId Slowbro { get; } = new("pret:SPECIES_SLOWBRO");
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } = [Slowbro];
    private static readonly ConditionalWeakTable<PokemonCatalog, SaffronGymOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<SilphOwnedPokemonCatalog, SaffronGymOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, SaffronGymOpponentPokemonCatalog> MechanicsProofs = new();
    public SilphOpponentPokemonCatalog Predecessor { get; }
    public SilphOwnedPokemonCatalog Owned => Predecessor.Owned;
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private SaffronGymOpponentPokemonCatalog(SilphOpponentPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static SaffronGymOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SaffronGymOpponentPokemonCatalog? For(SilphOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static SaffronGymOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SaffronGymOpponentPokemonCatalog Authenticate(SilphOpponentPokemonCatalog predecessor,
        ImmutableArray<PokemonSpeciesDefinition> opponentSpecies)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(SilphOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            opponentSpecies.IsDefault || opponentSpecies.Length != 1 || opponentSpecies[0]?.Id != Slowbro ||
            !SilphOwnedPokemonCatalog.MatchesSource(opponentSpecies[0], predecessor.Mechanics.Catalog.Species[Slowbro]))
            throw new ArgumentException("saffron_gym_opponent_source");
        var proof = new SaffronGymOpponentPokemonCatalog(predecessor,
            predecessor.Catalog with { Species = predecessor.Catalog.Species.Add(Slowbro, opponentSpecies[0]) });
        Proofs.Add(proof.Catalog, proof);
        Owners.GetValue(proof.Owned, _ => proof);
        MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof);
        return proof;
    }
}
