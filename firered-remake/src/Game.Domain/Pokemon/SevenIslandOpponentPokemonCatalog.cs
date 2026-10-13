using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
namespace Game.Domain.Pokemon;

/// <summary>The exact 180-species opponent union. Tyranitar keeps its inherited Rematch definition.</summary>
public sealed class SevenIslandOpponentPokemonCatalog
{
    public const string Profile = "firered-seven-island-opponents-v1";
    public static ImmutableArray<SpeciesId> TrainerOnlySpecies { get; } =
        [new("pret:SPECIES_GIRAFARIG"), new("pret:SPECIES_HITMONTOP"), new("pret:SPECIES_MILTANK"),
         new("pret:SPECIES_PINECO"), new("pret:SPECIES_SMEARGLE"), new("pret:SPECIES_URSARING")];
    private static readonly ConditionalWeakTable<PokemonCatalog, SevenIslandOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<SevenIslandOwnedPokemonCatalog, SevenIslandOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, SevenIslandOpponentPokemonCatalog> MechanicsProofs = new();
    public LeagueRematchOpponentPokemonCatalog Predecessor { get; }
    public SevenIslandOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private SevenIslandOpponentPokemonCatalog(LeagueRematchOpponentPokemonCatalog predecessor,
        SevenIslandOwnedPokemonCatalog owned, PokemonCatalog catalog)
    { Predecessor = predecessor; Owned = owned; Catalog = catalog; }
    public static SevenIslandOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SevenIslandOpponentPokemonCatalog? For(SevenIslandOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static SevenIslandOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static SevenIslandOpponentPokemonCatalog Authenticate(LeagueRematchOpponentPokemonCatalog predecessor,
        SevenIslandOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(LeagueRematchOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(SevenIslandOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 168 || owned.Catalog.Species.Count != 146)
            throw new ArgumentException("seven_island_opponent_predecessor");
        var species = predecessor.Catalog.Species;
        foreach (var id in SevenIslandOwnedPokemonCatalog.AdditionalSpecies)
            if (!species.ContainsKey(id)) species = species.Add(id, owned.Catalog.Species[id]);
        foreach (var id in TrainerOnlySpecies)
        {
            if (species.ContainsKey(id) || owned.Catalog.Species.ContainsKey(id)) throw new ArgumentException("seven_island_opponent_overlap");
            species = species.Add(id, owned.Mechanics.Catalog.Species[id]);
        }
        if (species.Count != 180 || !owned.Catalog.Species.Keys.All(species.ContainsKey)) throw new ArgumentException("seven_island_opponent_scope");
        var proof = new SevenIslandOpponentPokemonCatalog(predecessor, owned, predecessor.Catalog with { Species = species });
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof); MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof);
        return proof;
    }
}
