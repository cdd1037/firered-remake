using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Adds three stone targets to the ordinary opponent union, retaining all 192 prior definitions.</summary>
public sealed class StoneEvolutionOpponentPokemonCatalog
{
    public const string Profile = "firered-stone-evolution-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_POLIWRATH"), new("pret:SPECIES_VAPOREON"), new("pret:SPECIES_FLAREON")];
    private static readonly ConditionalWeakTable<PokemonCatalog, StoneEvolutionOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<StoneEvolutionOwnedPokemonCatalog, StoneEvolutionOpponentPokemonCatalog> Owners = new();
    public SuperRodOpponentPokemonCatalog Predecessor { get; }
    public StoneEvolutionOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private StoneEvolutionOpponentPokemonCatalog(SuperRodOpponentPokemonCatalog predecessor, StoneEvolutionOwnedPokemonCatalog owned)
    { Predecessor = predecessor; Owned = owned; Catalog = predecessor.Catalog with {
        Species = predecessor.Catalog.Species.AddRange(AdditionalSpecies.Select(id => KeyValuePair.Create(id, owned.Catalog.Species[id]))) }; }
    public static StoneEvolutionOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static StoneEvolutionOpponentPokemonCatalog? For(StoneEvolutionOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    public static StoneEvolutionOpponentPokemonCatalog Authenticate(SuperRodOpponentPokemonCatalog predecessor, StoneEvolutionOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(SuperRodOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(StoneEvolutionOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || predecessor.Catalog.Species.Count != 192 ||
            owned.Catalog.Species.Count != 170 || AdditionalSpecies.Any(predecessor.Catalog.Species.ContainsKey) ||
            !owned.Catalog.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies) ||
            StoneEvolutionOwnedPokemonCatalog.AdditionalSpecies.Except(AdditionalSpecies).Any(id =>
                !SilphOwnedPokemonCatalog.MatchesSource(predecessor.Catalog.Species[id], owned.Catalog.Species[id])))
            throw new ArgumentException("stone_evolution_opponent_predecessor");
        var proof = new StoneEvolutionOpponentPokemonCatalog(predecessor, owned);
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        OwnedBattlePokemonCatalog.BindSuccessorOpponents(owned.SharedBattle, proof.Catalog, Profile);
        return proof;
    }
}
