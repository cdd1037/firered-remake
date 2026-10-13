using System.Runtime.CompilerServices;

namespace Game.Domain.Pokemon;

/// <summary>Six153 plus three owned and three trainer-only species gives159 unique source species.</summary>
public sealed class FiveIslandOpponentPokemonCatalog
{
    public static System.Collections.Immutable.ImmutableArray<Game.Domain.Ids.SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_HOUNDOUR"), new("pret:SPECIES_HOUNDOOM"), new("pret:SPECIES_PORYGON")];
    public const string Profile = "firered-five-island-opponents-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, FiveIslandOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<FiveIslandOwnedPokemonCatalog, FiveIslandOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, FiveIslandOpponentPokemonCatalog> MechanicsProofs = new();
    public SixIslandOpponentPokemonCatalog Predecessor { get; }
    public FiveIslandOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private FiveIslandOpponentPokemonCatalog(SixIslandOpponentPokemonCatalog predecessor,
        FiveIslandOwnedPokemonCatalog owned, PokemonCatalog catalog)
    { Predecessor = predecessor; Owned = owned; Catalog = catalog; }
    public static FiveIslandOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static FiveIslandOpponentPokemonCatalog? For(FiveIslandOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static FiveIslandOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static FiveIslandOpponentPokemonCatalog Authenticate(SixIslandOpponentPokemonCatalog predecessor, FiveIslandOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(SixIslandOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(FiveIslandOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 153 ||
            !ReferenceEquals(predecessor.Catalog.Moves, owned.Catalog.Moves))
            throw new ArgumentException("five_island_opponent_predecessor");
        var species = predecessor.Catalog.Species;
        foreach (var (id, value) in owned.Catalog.Species)
            if (!species.TryGetValue(id, out var inherited)) species = species.Add(id, value);
            else if (!ReferenceEquals(inherited, value) && !FiveIslandOwnedPokemonCatalog.MatchesSource(inherited, value))
                throw new ArgumentException("five_island_opponent_overlap_source");
        foreach (var id in AdditionalSpecies)
        {
            if (species.ContainsKey(id)) throw new ArgumentException("five_island_opponent_addition_overlap");
            species = species.Add(id, owned.Mechanics.Catalog.Species[id]);
        }
        var catalog = predecessor.Catalog with { Species = species };
        if (catalog.Species.Count != 159) throw new ArgumentException("five_island_opponent_scope");
        var proof = new FiveIslandOpponentPokemonCatalog(predecessor, owned, catalog);
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof); return proof;
    }
}
