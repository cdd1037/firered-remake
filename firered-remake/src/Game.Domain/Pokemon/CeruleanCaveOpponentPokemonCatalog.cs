using System.Runtime.CompilerServices;

namespace Game.Domain.Pokemon;

/// <summary>The existing 162 opponent definitions plus Mewtwo; no trainer roster is expanded.</summary>
public sealed class CeruleanCaveOpponentPokemonCatalog
{
    public const string Profile = "firered-cerulean-cave-opponents-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, CeruleanCaveOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<CeruleanCaveOwnedPokemonCatalog, CeruleanCaveOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, CeruleanCaveOpponentPokemonCatalog> MechanicsProofs = new();
    public CelioOpponentPokemonCatalog Predecessor { get; }
    public CeruleanCaveOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private CeruleanCaveOpponentPokemonCatalog(CelioOpponentPokemonCatalog predecessor, CeruleanCaveOwnedPokemonCatalog owned, PokemonCatalog catalog)
    { Predecessor = predecessor; Owned = owned; Catalog = catalog; }
    public static CeruleanCaveOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static CeruleanCaveOpponentPokemonCatalog? For(CeruleanCaveOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static CeruleanCaveOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static CeruleanCaveOpponentPokemonCatalog Authenticate(CelioOpponentPokemonCatalog predecessor, CeruleanCaveOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(CelioOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(CeruleanCaveOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 162 ||
            !ReferenceEquals(predecessor.Catalog.Moves, owned.Catalog.Moves))
            throw new ArgumentException("cerulean_cave_opponent_predecessor");
        var species = predecessor.Catalog.Species;
        foreach (var id in CeruleanCaveOwnedPokemonCatalog.AdditionalSpecies)
        {
            if (species.ContainsKey(id)) throw new ArgumentException("cerulean_cave_opponent_addition_overlap");
            species = species.Add(id, owned.Catalog.Species[id]);
        }
        if (species.Count != 163 || !owned.Catalog.Species.Keys.All(species.ContainsKey))
            throw new ArgumentException("cerulean_cave_opponent_scope");
        var proof = new CeruleanCaveOpponentPokemonCatalog(predecessor, owned, predecessor.Catalog with { Species = species });
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof); return proof;
    }
}
