using System.Runtime.CompilerServices;

namespace Game.Domain.Pokemon;

/// <summary>The 191 ordinary opponent union, adding Articuno and retaining all prior source definitions.</summary>
public sealed class SeafoamOpponentPokemonCatalog
{
    public const string Profile = "firered-seafoam-opponents-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, SeafoamOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<SeafoamOwnedPokemonCatalog, SeafoamOpponentPokemonCatalog> Owners = new();
    public NpcTradeOpponentPokemonCatalog Predecessor { get; }
    public SeafoamOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private SeafoamOpponentPokemonCatalog(NpcTradeOpponentPokemonCatalog predecessor, SeafoamOwnedPokemonCatalog owned)
    { Predecessor = predecessor; Owned = owned; Catalog = predecessor.Catalog with { Species = predecessor.Catalog.Species.Add(SeafoamOwnedPokemonCatalog.ArticunoId, owned.Catalog.Species[SeafoamOwnedPokemonCatalog.ArticunoId]) }; }
    public static SeafoamOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SeafoamOpponentPokemonCatalog? For(SeafoamOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    public static SeafoamOpponentPokemonCatalog Authenticate(NpcTradeOpponentPokemonCatalog predecessor, SeafoamOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(NpcTradeOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(SeafoamOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 190 ||
            owned.Catalog.Species.Count != 162 || predecessor.Catalog.Species.ContainsKey(SeafoamOwnedPokemonCatalog.ArticunoId) ||
            !owned.Predecessor.Catalog.Species.Keys.All(predecessor.Catalog.Species.ContainsKey))
            throw new ArgumentException("seafoam_opponent_predecessor");
        var proof = new SeafoamOpponentPokemonCatalog(predecessor, owned);
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        OwnedBattlePokemonCatalog.BindSuccessorOpponents(owned.SharedBattle, proof.Catalog, Profile);
        return proof;
    }
}
