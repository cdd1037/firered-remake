using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Adds Blissey to the ordinary opponent union; the three other opponents retain identity.</summary>
public sealed class OrdinaryEvolutionOpponentPokemonCatalog
{
    public const string Profile = "firered-ordinary-evolution-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } = [new("pret:SPECIES_BLISSEY")];
    private static readonly ConditionalWeakTable<PokemonCatalog, OrdinaryEvolutionOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<OrdinaryEvolutionOwnedPokemonCatalog, OrdinaryEvolutionOpponentPokemonCatalog> Owners = new();
    public BreedingOpponentPokemonCatalog Predecessor { get; }
    public OrdinaryEvolutionOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }

    private OrdinaryEvolutionOpponentPokemonCatalog(BreedingOpponentPokemonCatalog predecessor, OrdinaryEvolutionOwnedPokemonCatalog owned)
    { Predecessor = predecessor; Owned = owned; Catalog = predecessor.Catalog with {
        Species = predecessor.Catalog.Species.AddRange(AdditionalSpecies.Select(id => KeyValuePair.Create(id, owned.Catalog.Species[id]))) }; }

    public static OrdinaryEvolutionOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static OrdinaryEvolutionOpponentPokemonCatalog? For(OrdinaryEvolutionOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;

    public static OrdinaryEvolutionOpponentPokemonCatalog Authenticate(BreedingOpponentPokemonCatalog predecessor, OrdinaryEvolutionOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(BreedingOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(OrdinaryEvolutionOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || !ReferenceEquals(predecessor.Mechanics, owned.Mechanics) ||
            predecessor.Catalog.Species.Count != 203 || owned.Catalog.Species.Count != 186 ||
            !ReferenceEquals(predecessor.Catalog.Moves, owned.Catalog.Moves) || AdditionalSpecies.Any(predecessor.Catalog.Species.ContainsKey) ||
            !owned.Catalog.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("ordinary_evolution_opponent_predecessor");
        foreach (var id in OrdinaryEvolutionOwnedPokemonCatalog.AdditionalSpecies.Except(AdditionalSpecies))
        {
            var canonical = owned.Mechanics.Catalog.Species[id];
            // Opponent projections retain their prior evolution metadata; only the owned
            // catalog gains its new normal edges. All other source fields must match.
            if (!SilphOwnedPokemonCatalog.MatchesSource(predecessor.Catalog.Species[id] with { Evolutions = canonical.Evolutions }, canonical))
                throw new ArgumentException("ordinary_evolution_opponent_source");
        }
        var proof = new OrdinaryEvolutionOpponentPokemonCatalog(predecessor, owned);
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        OwnedBattlePokemonCatalog.BindSuccessorOpponents(owned.SharedBattle, proof.Catalog, Profile);
        return proof;
    }
}
