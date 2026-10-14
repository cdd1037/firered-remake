using System.Runtime.CompilerServices;

namespace Game.Domain.Pokemon;

/// <summary>The same 190 ordinary opponents, bound to the original-trade owner.</summary>
public sealed class NpcTradeOpponentPokemonCatalog
{
    public const string Profile = "firered-npc-trade-opponents-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, NpcTradeOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<NpcTradeOwnedPokemonCatalog, NpcTradeOpponentPokemonCatalog> Owners = new();
    public FossilLabOpponentPokemonCatalog Predecessor { get; }
    public NpcTradeOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private NpcTradeOpponentPokemonCatalog(FossilLabOpponentPokemonCatalog predecessor, NpcTradeOwnedPokemonCatalog owned)
    { Predecessor = predecessor; Owned = owned; Catalog = predecessor.Catalog with { }; }
    public static NpcTradeOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static NpcTradeOpponentPokemonCatalog? For(NpcTradeOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    public static NpcTradeOpponentPokemonCatalog Authenticate(FossilLabOpponentPokemonCatalog predecessor, NpcTradeOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(FossilLabOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(NpcTradeOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 190 ||
            owned.Catalog.Species.Count != 161 || !owned.Catalog.Species.Keys.All(predecessor.Catalog.Species.ContainsKey) ||
            NpcTradeOwnedPokemonCatalog.AdditionalSpecies.Any(id =>
                !SilphOwnedPokemonCatalog.MatchesSource(predecessor.Catalog.Species[id], owned.Catalog.Species[id])))
            throw new ArgumentException("npc_trade_opponent_predecessor");
        var proof = new NpcTradeOpponentPokemonCatalog(predecessor, owned);
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        OwnedBattlePokemonCatalog.BindSuccessorOpponents(owned.SharedBattle, proof.Catalog, Profile);
        return proof;
    }
}
