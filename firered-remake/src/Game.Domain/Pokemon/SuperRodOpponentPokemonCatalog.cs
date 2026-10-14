using System.Runtime.CompilerServices;

namespace Game.Domain.Pokemon;

/// <summary>The 192 ordinary opponent union, adding Qwilfish and retaining all prior source definitions.</summary>
public sealed class SuperRodOpponentPokemonCatalog
{
    public const string Profile = "firered-super-rod-opponents-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, SuperRodOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<SuperRodOwnedPokemonCatalog, SuperRodOpponentPokemonCatalog> Owners = new();
    public SeafoamOpponentPokemonCatalog Predecessor { get; }
    public SuperRodOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private SuperRodOpponentPokemonCatalog(SeafoamOpponentPokemonCatalog predecessor, SuperRodOwnedPokemonCatalog owned)
    { Predecessor = predecessor; Owned = owned; Catalog = predecessor.Catalog with { Species = predecessor.Catalog.Species.Add(SuperRodOwnedPokemonCatalog.QwilfishId, owned.Catalog.Species[SuperRodOwnedPokemonCatalog.QwilfishId]) }; }
    public static SuperRodOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SuperRodOpponentPokemonCatalog? For(SuperRodOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    public static SuperRodOpponentPokemonCatalog Authenticate(SeafoamOpponentPokemonCatalog predecessor, SuperRodOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(SeafoamOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(SuperRodOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 191 ||
            owned.Catalog.Species.Count != 163 || predecessor.Catalog.Species.ContainsKey(SuperRodOwnedPokemonCatalog.QwilfishId) ||
            !owned.Predecessor.Catalog.Species.Keys.All(predecessor.Catalog.Species.ContainsKey))
            throw new ArgumentException("super_rod_opponent_predecessor");
        var proof = new SuperRodOpponentPokemonCatalog(predecessor, owned);
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        OwnedBattlePokemonCatalog.BindSuccessorOpponents(owned.SharedBattle, proof.Catalog, Profile);
        return proof;
    }
}
