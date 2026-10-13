using System.Runtime.CompilerServices;

namespace Game.Domain.Pokemon;

/// <summary>Four141 plus seven newly owned and five trainer-only species gives153 unique species.</summary>
public sealed class SixIslandOpponentPokemonCatalog
{
    public static System.Collections.Immutable.ImmutableArray<Game.Domain.Ids.SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_SUNKERN"), new("pret:SPECIES_SUNFLORA"), new("pret:SPECIES_CHINCHOU"), new("pret:SPECIES_LANTURN"), new("pret:SPECIES_SUDOWOODO")];
    public const string Profile = "firered-six-island-opponents-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, SixIslandOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<SixIslandOwnedPokemonCatalog, SixIslandOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, SixIslandOpponentPokemonCatalog> MechanicsProofs = new();
    public FourIslandOpponentPokemonCatalog Predecessor { get; }
    public SixIslandOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private SixIslandOpponentPokemonCatalog(FourIslandOpponentPokemonCatalog predecessor,
        SixIslandOwnedPokemonCatalog owned, PokemonCatalog catalog)
    { Predecessor = predecessor; Owned = owned; Catalog = catalog; }
    public static SixIslandOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SixIslandOpponentPokemonCatalog? For(SixIslandOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static SixIslandOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SixIslandOpponentPokemonCatalog Authenticate(FourIslandOpponentPokemonCatalog predecessor, SixIslandOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(FourIslandOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(SixIslandOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 141 ||
            !ReferenceEquals(predecessor.Catalog.Moves, owned.Catalog.Moves))
            throw new ArgumentException("six_island_opponent_predecessor");
        var species = predecessor.Catalog.Species;
        foreach (var (id, value) in owned.Catalog.Species)
            if (!species.TryGetValue(id, out var inherited)) species = species.Add(id, value);
            else if (!ReferenceEquals(inherited, value) && !SixIslandOwnedPokemonCatalog.MatchesSource(inherited, value))
                throw new ArgumentException("six_island_opponent_overlap_source");
        foreach (var id in AdditionalSpecies)
        {
            if (species.ContainsKey(id)) throw new ArgumentException("six_island_opponent_addition_overlap");
            species = species.Add(id, owned.Mechanics.Catalog.Species[id]);
        }
        var catalog = predecessor.Catalog with { Species = species };
        if (catalog.Species.Count != 153) throw new ArgumentException("six_island_opponent_scope");
        var proof = new SixIslandOpponentPokemonCatalog(predecessor, owned, catalog);
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof); return proof;
    }
}
