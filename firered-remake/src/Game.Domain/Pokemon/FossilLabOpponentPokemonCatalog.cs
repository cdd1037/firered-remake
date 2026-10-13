using System.Runtime.CompilerServices;

namespace Game.Domain.Pokemon;

/// <summary>The ordinary 190-species opponent union. Trainer Tower keeps its separate roster.</summary>
public sealed class FossilLabOpponentPokemonCatalog
{
    public const string Profile = "firered-fossil-lab-opponents-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, FossilLabOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<FossilLabOwnedPokemonCatalog, FossilLabOpponentPokemonCatalog> Owners = new();
    public FightingDojoOpponentPokemonCatalog Predecessor { get; }
    public FossilLabOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private FossilLabOpponentPokemonCatalog(FightingDojoOpponentPokemonCatalog predecessor,
        FossilLabOwnedPokemonCatalog owned, PokemonCatalog catalog)
    { Predecessor = predecessor; Owned = owned; Catalog = catalog; }
    public static FossilLabOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static FossilLabOpponentPokemonCatalog? For(FossilLabOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    public static FossilLabOpponentPokemonCatalog Authenticate(FightingDojoOpponentPokemonCatalog predecessor, FossilLabOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(FightingDojoOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(FossilLabOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 186 ||
            owned.Catalog.Species.Count != 159 ||
            FossilLabOwnedPokemonCatalog.AdditionalSpecies.Take(4).Any(predecessor.Catalog.Species.ContainsKey) ||
            !predecessor.Catalog.Species.TryGetValue(FossilLabOwnedPokemonCatalog.AerodactylId, out var amber) ||
            !SilphOwnedPokemonCatalog.MatchesSource(amber, owned.Catalog.Species[FossilLabOwnedPokemonCatalog.AerodactylId]))
            throw new ArgumentException("fossil_lab_opponent_predecessor");
        var species = predecessor.Catalog.Species;
        foreach (var id in FossilLabOwnedPokemonCatalog.AdditionalSpecies.Take(4)) species = species.Add(id, owned.Catalog.Species[id]);
        if (species.Count != 190 || !owned.Catalog.Species.Keys.All(species.ContainsKey))
            throw new ArgumentException("fossil_lab_opponent_scope");
        var proof = new FossilLabOpponentPokemonCatalog(predecessor, owned, predecessor.Catalog with { Species = species });
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        OwnedBattlePokemonCatalog.BindSuccessorOpponents(owned.SharedBattle, proof.Catalog, Profile);
        return proof;
    }
}
