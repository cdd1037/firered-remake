using System.Runtime.CompilerServices;
namespace Game.Domain.Pokemon;

/// <summary>The exact 183-species current opponent union, retaining all 180 predecessor definitions.</summary>
public sealed class EventIslandOpponentPokemonCatalog
{
    public const string Profile = "firered-event-island-opponents-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, EventIslandOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<EventIslandOwnedPokemonCatalog, EventIslandOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, EventIslandOpponentPokemonCatalog> MechanicsProofs = new();
    public SevenIslandOpponentPokemonCatalog Predecessor { get; }
    public EventIslandOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private EventIslandOpponentPokemonCatalog(SevenIslandOpponentPokemonCatalog predecessor, EventIslandOwnedPokemonCatalog owned, PokemonCatalog catalog)
    { Predecessor = predecessor; Owned = owned; Catalog = catalog; }
    public static EventIslandOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static EventIslandOpponentPokemonCatalog? For(EventIslandOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static EventIslandOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static EventIslandOpponentPokemonCatalog Authenticate(SevenIslandOpponentPokemonCatalog predecessor, EventIslandOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(SevenIslandOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(EventIslandOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 180 || owned.Catalog.Species.Count != 149)
            throw new ArgumentException("event_island_opponent_predecessor");
        var species = predecessor.Catalog.Species;
        foreach (var id in EventIslandOwnedPokemonCatalog.AdditionalSpecies) species = species.Add(id, owned.Catalog.Species[id]);
        if (species.Count != 183 || !owned.Catalog.Species.Keys.All(species.ContainsKey)) throw new ArgumentException("event_island_opponent_scope");
        var proof = new EventIslandOpponentPokemonCatalog(predecessor, owned, predecessor.Catalog with { Species = species });
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof); MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof);
        return proof;
    }
}
