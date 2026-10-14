using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Adds only Dratini to the ordinary union, preserving all 195 predecessor definitions.</summary>
public sealed class GameCornerOpponentPokemonCatalog
{
    public const string Profile = "firered-game-corner-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } = [GameCornerOwnedPokemonCatalog.AdditionalSpecies[0]];
    private static readonly ConditionalWeakTable<PokemonCatalog, GameCornerOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<GameCornerOwnedPokemonCatalog, GameCornerOpponentPokemonCatalog> Owners = new();
    public StoneEvolutionOpponentPokemonCatalog Predecessor { get; }
    public GameCornerOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }

    private GameCornerOpponentPokemonCatalog(StoneEvolutionOpponentPokemonCatalog predecessor, GameCornerOwnedPokemonCatalog owned)
    { Predecessor = predecessor; Owned = owned; Catalog = predecessor.Catalog with {
        Species = predecessor.Catalog.Species.Add(AdditionalSpecies[0], owned.Catalog.Species[AdditionalSpecies[0]]) }; }

    public static GameCornerOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static GameCornerOpponentPokemonCatalog? For(GameCornerOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;

    public static GameCornerOpponentPokemonCatalog Authenticate(StoneEvolutionOpponentPokemonCatalog predecessor, GameCornerOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(StoneEvolutionOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(GameCornerOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || !ReferenceEquals(predecessor.Mechanics, owned.Mechanics) ||
            predecessor.Catalog.Species.Count != 195 || owned.Catalog.Species.Count != 174 ||
            !ReferenceEquals(predecessor.Catalog.Moves, owned.Catalog.Moves) || AdditionalSpecies.Any(predecessor.Catalog.Species.ContainsKey) ||
            !owned.Catalog.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("game_corner_opponent_predecessor");
        foreach (var id in GameCornerOwnedPokemonCatalog.AdditionalSpecies.Except(AdditionalSpecies))
        {
            var canonical = owned.Mechanics.Catalog.Species[id];
            // Opponent projections retain their prior evolution metadata; only the owned
            // catalog gains the two ordinary level edges. All other source fields must match.
            if (!SilphOwnedPokemonCatalog.MatchesSource(predecessor.Catalog.Species[id] with { Evolutions = canonical.Evolutions }, canonical))
                throw new ArgumentException("game_corner_opponent_source");
        }
        var proof = new GameCornerOpponentPokemonCatalog(predecessor, owned);
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        OwnedBattlePokemonCatalog.BindSuccessorOpponents(owned.SharedBattle, proof.Catalog, Profile);
        return proof;
    }
}
