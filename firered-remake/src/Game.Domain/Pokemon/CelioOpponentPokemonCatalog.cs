using System.Runtime.CompilerServices;

namespace Game.Domain.Pokemon;

/// <summary>The existing159 opponents plus three wild roaming species; no trainer roster is expanded.</summary>
public sealed class CelioOpponentPokemonCatalog
{
    public const string Profile = "firered-celio-opponents-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, CelioOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<CelioOwnedPokemonCatalog, CelioOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, CelioOpponentPokemonCatalog> MechanicsProofs = new();
    public FiveIslandOpponentPokemonCatalog Predecessor { get; }
    public CelioOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private CelioOpponentPokemonCatalog(FiveIslandOpponentPokemonCatalog predecessor, CelioOwnedPokemonCatalog owned, PokemonCatalog catalog)
    { Predecessor = predecessor; Owned = owned; Catalog = catalog; }
    public static CelioOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static CelioOpponentPokemonCatalog? For(CelioOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static CelioOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static CelioOpponentPokemonCatalog Authenticate(FiveIslandOpponentPokemonCatalog predecessor, CelioOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(FiveIslandOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(CelioOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 159 ||
            !ReferenceEquals(predecessor.Catalog.Moves, owned.Catalog.Moves))
            throw new ArgumentException("celio_opponent_predecessor");
        var species = predecessor.Catalog.Species;
        foreach (var id in CelioOwnedPokemonCatalog.AdditionalSpecies)
        {
            if (species.ContainsKey(id)) throw new ArgumentException("celio_opponent_addition_overlap");
            species = species.Add(id, owned.Catalog.Species[id]);
        }
        if (species.Count != 162 || !owned.Catalog.Species.Keys.All(species.ContainsKey))
            throw new ArgumentException("celio_opponent_scope");
        var proof = new CelioOpponentPokemonCatalog(predecessor, owned, predecessor.Catalog with { Species = species });
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof); return proof;
    }
}
