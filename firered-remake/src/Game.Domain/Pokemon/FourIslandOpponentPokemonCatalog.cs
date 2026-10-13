using System.Runtime.CompilerServices;

namespace Game.Domain.Pokemon;

/// <summary>Ruby135 plus owned125 gives141 unique species. Dewgong is promoted, not counted twice.</summary>
public sealed class FourIslandOpponentPokemonCatalog
{
    public const string Profile = "firered-four-island-opponents-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, FourIslandOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<FourIslandOwnedPokemonCatalog, FourIslandOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, FourIslandOpponentPokemonCatalog> MechanicsProofs = new();
    public SeviiRubyOpponentPokemonCatalog Predecessor { get; }
    public FourIslandOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private FourIslandOpponentPokemonCatalog(SeviiRubyOpponentPokemonCatalog predecessor,
        FourIslandOwnedPokemonCatalog owned, PokemonCatalog catalog)
    { Predecessor = predecessor; Owned = owned; Catalog = catalog; }
    public static FourIslandOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static FourIslandOpponentPokemonCatalog? For(FourIslandOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static FourIslandOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static FourIslandOpponentPokemonCatalog Authenticate(SeviiRubyOpponentPokemonCatalog predecessor, FourIslandOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(SeviiRubyOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(FourIslandOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 135 ||
            !ReferenceEquals(predecessor.Catalog.Moves, owned.Catalog.Moves))
            throw new ArgumentException("four_island_opponent_predecessor");
        var species = predecessor.Catalog.Species;
        foreach (var (id, value) in owned.Catalog.Species)
            if (!species.TryGetValue(id, out var inherited)) species = species.Add(id, value);
            else if (!ReferenceEquals(inherited, value) && !SilphOwnedPokemonCatalog.MatchesSource(inherited, value))
                throw new ArgumentException("four_island_opponent_overlap_source");
        var catalog = predecessor.Catalog with { Species = species };
        if (catalog.Species.Count != 141) throw new ArgumentException("four_island_opponent_scope");
        var proof = new FourIslandOpponentPokemonCatalog(predecessor, owned, catalog);
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof); return proof;
    }
}
