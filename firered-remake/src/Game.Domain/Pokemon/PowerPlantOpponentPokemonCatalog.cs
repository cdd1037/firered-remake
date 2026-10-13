using System.Runtime.CompilerServices;
namespace Game.Domain.Pokemon;

/// <summary>The exact 186-species current opponent union, retaining all 183 predecessor definitions.</summary>
public sealed class PowerPlantOpponentPokemonCatalog
{
    public const string Profile = "firered-power-plant-opponents-v1";
    private static readonly ConditionalWeakTable<PokemonCatalog, PowerPlantOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<PowerPlantOwnedPokemonCatalog, PowerPlantOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, PowerPlantOpponentPokemonCatalog> MechanicsProofs = new();
    public EventIslandOpponentPokemonCatalog Predecessor { get; }
    public PowerPlantOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private PowerPlantOpponentPokemonCatalog(EventIslandOpponentPokemonCatalog predecessor, PowerPlantOwnedPokemonCatalog owned, PokemonCatalog catalog)
    { Predecessor = predecessor; Owned = owned; Catalog = catalog; }
    public static PowerPlantOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static PowerPlantOpponentPokemonCatalog? For(PowerPlantOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static PowerPlantOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static PowerPlantOpponentPokemonCatalog Authenticate(EventIslandOpponentPokemonCatalog predecessor, PowerPlantOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(EventIslandOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(PowerPlantOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 183 || owned.Catalog.Species.Count != 152)
            throw new ArgumentException("power_plant_opponent_predecessor");
        var species = predecessor.Catalog.Species;
        foreach (var id in PowerPlantOwnedPokemonCatalog.AdditionalSpecies) species = species.Add(id, owned.Catalog.Species[id]);
        if (species.Count != 186 || !owned.Catalog.Species.Keys.All(species.ContainsKey)) throw new ArgumentException("power_plant_opponent_scope");
        var proof = new PowerPlantOpponentPokemonCatalog(predecessor, owned, predecessor.Catalog with { Species = species });
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof); MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof);
        return proof;
    }
}
