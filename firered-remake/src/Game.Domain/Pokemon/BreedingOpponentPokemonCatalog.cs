using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Adds seven babies to the ordinary union; the Hitmontop opponent already exists.</summary>
public sealed class BreedingOpponentPokemonCatalog
{
    public const string Profile = "firered-breeding-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } = [new("pret:SPECIES_PICHU"), new("pret:SPECIES_CLEFFA"), new("pret:SPECIES_IGGLYBUFF"), new("pret:SPECIES_TYROGUE"), new("pret:SPECIES_SMOOCHUM"), new("pret:SPECIES_ELEKID"), new("pret:SPECIES_WYNAUT")];
    private static readonly ConditionalWeakTable<PokemonCatalog, BreedingOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<BreedingOwnedPokemonCatalog, BreedingOpponentPokemonCatalog> Owners = new();
    public GameCornerOpponentPokemonCatalog Predecessor { get; }
    public BreedingOwnedPokemonCatalog Owned { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }

    private BreedingOpponentPokemonCatalog(GameCornerOpponentPokemonCatalog predecessor, BreedingOwnedPokemonCatalog owned)
    { Predecessor = predecessor; Owned = owned; Catalog = predecessor.Catalog with {
        Species = predecessor.Catalog.Species.AddRange(AdditionalSpecies.Select(id => KeyValuePair.Create(id, owned.Catalog.Species[id]))) }; }

    public static BreedingOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static BreedingOpponentPokemonCatalog? For(BreedingOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;

    public static BreedingOpponentPokemonCatalog Authenticate(GameCornerOpponentPokemonCatalog predecessor, BreedingOwnedPokemonCatalog owned)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(owned);
        if (!ReferenceEquals(GameCornerOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(BreedingOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(predecessor.Owned, owned.Predecessor) || !ReferenceEquals(predecessor.Mechanics, owned.Mechanics) ||
            predecessor.Catalog.Species.Count != 196 || owned.Catalog.Species.Count != 182 ||
            !ReferenceEquals(predecessor.Catalog.Moves, owned.Catalog.Moves) || AdditionalSpecies.Any(predecessor.Catalog.Species.ContainsKey) ||
            !owned.Catalog.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("breeding_opponent_predecessor");
        foreach (var id in BreedingOwnedPokemonCatalog.AdditionalSpecies.Except(AdditionalSpecies))
        {
            var canonical = owned.Mechanics.Catalog.Species[id];
            // Opponent projections retain their prior evolution metadata; only the owned
            // catalog gains the two ordinary level edges. All other source fields must match.
            if (!SilphOwnedPokemonCatalog.MatchesSource(predecessor.Catalog.Species[id] with { Evolutions = canonical.Evolutions }, canonical))
                throw new ArgumentException("breeding_opponent_source");
        }
        var proof = new BreedingOpponentPokemonCatalog(predecessor, owned);
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(owned, _ => proof);
        OwnedBattlePokemonCatalog.BindSuccessorOpponents(owned.SharedBattle, proof.Catalog, Profile);
        return proof;
    }
}
