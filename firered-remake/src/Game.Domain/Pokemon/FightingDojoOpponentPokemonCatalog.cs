using System.Runtime.CompilerServices;
namespace Game.Domain.Pokemon;

/// <summary>The same 186 opponent definitions, now bound to the authenticated 154-species owner.</summary>
public sealed class FightingDojoOpponentPokemonCatalog
{
    public const string Profile = "firered-fighting-dojo-opponents-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, FightingDojoOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<FightingDojoOwnedPokemonCatalog, FightingDojoOpponentPokemonCatalog> Owners = new();
    public PowerPlantOpponentPokemonCatalog Predecessor { get; }
    public FightingDojoOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private FightingDojoOpponentPokemonCatalog(PowerPlantOpponentPokemonCatalog predecessor, FightingDojoOwnedPokemonCatalog owned)
    { Predecessor = predecessor; Owned = owned; Catalog = predecessor.Catalog with { }; }
    public static FightingDojoOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static FightingDojoOpponentPokemonCatalog? For(FightingDojoOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    public static FightingDojoOpponentPokemonCatalog Authenticate(PowerPlantOpponentPokemonCatalog predecessor, FightingDojoOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(PowerPlantOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(FightingDojoOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 186 ||
            owned.Catalog.Species.Count != 154 || !owned.Catalog.Species.Keys.All(predecessor.Catalog.Species.ContainsKey) ||
            FightingDojoOwnedPokemonCatalog.AdditionalSpecies.Any(id =>
                !SilphOwnedPokemonCatalog.MatchesSource(predecessor.Catalog.Species[id], owned.Catalog.Species[id])))
            throw new ArgumentException("fighting_dojo_opponent_predecessor");
        var proof = new FightingDojoOpponentPokemonCatalog(predecessor, owned);
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        OwnedBattlePokemonCatalog.BindSuccessorOpponents(owned.SharedBattle, proof.Catalog, Profile);
        return proof;
    }
}
