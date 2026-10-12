using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Four exact new trainer species; inherited opponent and ownership identities survive.</summary>
public sealed class CinnabarOpponentPokemonCatalog
{
    public const string Profile = "firered-cinnabar-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_NINETALES"), new("pret:SPECIES_ARCANINE"), new("pret:SPECIES_RAPIDASH"), new("pret:SPECIES_CLOYSTER")];
    private static readonly ConditionalWeakTable<PokemonCatalog, CinnabarOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<CinnabarOwnedPokemonCatalog, CinnabarOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, CinnabarOpponentPokemonCatalog> MechanicsProofs = new();
    public CinnabarOwnedPokemonCatalog Owned { get; }
    public SaffronGymOpponentPokemonCatalog Predecessor { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private CinnabarOpponentPokemonCatalog(CinnabarOwnedPokemonCatalog owned, SaffronGymOpponentPokemonCatalog predecessor, PokemonCatalog catalog)
    { Owned = owned; Predecessor = predecessor; Catalog = catalog; }
    public static CinnabarOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static CinnabarOpponentPokemonCatalog? For(CinnabarOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static CinnabarOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static CinnabarOpponentPokemonCatalog Authenticate(CinnabarOwnedPokemonCatalog owned,
        SaffronGymOpponentPokemonCatalog predecessor, ImmutableArray<PokemonSpeciesDefinition> opponentSpecies)
    {
        ArgumentNullException.ThrowIfNull(owned); ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(CinnabarOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(SaffronGymOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(owned.Predecessor, predecessor.Owned) || opponentSpecies.IsDefault || opponentSpecies.Length != 4 ||
            !opponentSpecies.Select(s => s?.Id).SequenceEqual(AdditionalSpecies.Select(s => (SpeciesId?)s)) ||
            opponentSpecies.Any(s => !SilphOwnedPokemonCatalog.MatchesSource(s, owned.Mechanics.Catalog.Species[s.Id])))
            throw new ArgumentException("cinnabar_opponent_source");
        var inherited = predecessor.Catalog.Species.Where(p => !predecessor.Owned.Catalog.Species.ContainsKey(p.Key));
        var proof = new CinnabarOpponentPokemonCatalog(owned, predecessor, owned.Catalog with {
            Species = owned.Catalog.Species.AddRange(inherited).AddRange(opponentSpecies.Select(s => KeyValuePair.Create(s.Id, s))) });
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof); return proof;
    }
}
