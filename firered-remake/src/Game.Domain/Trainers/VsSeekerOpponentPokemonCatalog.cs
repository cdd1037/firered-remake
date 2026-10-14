using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Trainers;

/// <summary>Three original opponent-only additions to the existing shared campaign owner.
/// Reuses canonical source definitions and the generic successor binding; ownership is unchanged.</summary>
public sealed class VsSeekerOpponentPokemonCatalog
{
    public const string Profile = "firered-vs-seeker-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_MAREEP"), new("pret:SPECIES_FLAAFFY"), new("pret:SPECIES_MARILL")];
    private static readonly ConditionalWeakTable<OwnedBattlePokemonCatalog, VsSeekerOpponentPokemonCatalog> Owners = new();
    public OwnedBattlePokemonCatalog Owned { get; }
    public PokemonCatalog Predecessor { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private VsSeekerOpponentPokemonCatalog(OwnedBattlePokemonCatalog owned, PokemonCatalog predecessor)
    {
        Owned = owned; Predecessor = predecessor;
        Catalog = predecessor with { Species = predecessor.Species.AddRange(
            AdditionalSpecies.Select(id => KeyValuePair.Create(id, owned.Mechanics.Catalog.Species[id]))) };
    }
    public static VsSeekerOpponentPokemonCatalog Authenticate(OwnedBattlePokemonCatalog owned, PokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(owned); ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(OwnedBattlePokemonCatalog.For(owned.Catalog), owned) ||
            predecessor.SourceCommit != TrainerPartyFactory.SourceCommit ||
            !ReferenceEquals(predecessor.Moves, owned.Catalog.Moves) ||
            AdditionalSpecies.Any(id => predecessor.Species.ContainsKey(id) || owned.Catalog.Species.ContainsKey(id)) ||
            !AdditionalSpecies.Select(id => owned.Mechanics.Catalog.Species[id].SourceSpeciesId).SequenceEqual(new[] { 179, 180, 183 }))
            throw new ArgumentException("vs_seeker_opponent_source");
        if (Owners.TryGetValue(owned, out var existing))
        {
            if (!ReferenceEquals(existing.Predecessor, predecessor)) throw new ArgumentException("vs_seeker_opponent_rebind");
            return existing;
        }
        var proof = new VsSeekerOpponentPokemonCatalog(owned, predecessor);
        // This shared boundary independently verifies the exact currently registered predecessor.
        OwnedBattlePokemonCatalog.BindOpponentSuccessor(owned, predecessor, owned.TrainerOpponentProfile, proof.Catalog, Profile);
        Owners.Add(owned, proof); return proof;
    }
}
