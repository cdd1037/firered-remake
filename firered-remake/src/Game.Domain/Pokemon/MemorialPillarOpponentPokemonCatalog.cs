using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Two original opponent-only species above ordinary 204. Ownership stays exactly 186.</summary>
public sealed class MemorialPillarOpponentPokemonCatalog
{
    public const string Profile = "firered-memorial-pillar-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_HOOTHOOT"), new("pret:SPECIES_NOCTOWL")];
    private static readonly ConditionalWeakTable<PokemonCatalog, MemorialPillarOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<OrdinaryEvolutionOwnedPokemonCatalog, MemorialPillarOpponentPokemonCatalog> Owners = new();
    public OrdinaryEvolutionOpponentPokemonCatalog Predecessor { get; }
    public OrdinaryEvolutionOwnedPokemonCatalog Owned => Predecessor.Owned;
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    public PokemonCatalog Catalog { get; }
    private MemorialPillarOpponentPokemonCatalog(OrdinaryEvolutionOpponentPokemonCatalog predecessor)
    {
        Predecessor = predecessor;
        Catalog = predecessor.Catalog with { Species = predecessor.Catalog.Species.AddRange(
            AdditionalSpecies.Select(id => KeyValuePair.Create(id, predecessor.Mechanics.Catalog.Species[id]))) };
    }
    public static MemorialPillarOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static MemorialPillarOpponentPokemonCatalog? For(OrdinaryEvolutionOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    public static MemorialPillarOpponentPokemonCatalog Authenticate(OrdinaryEvolutionOpponentPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(OrdinaryEvolutionOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            !ReferenceEquals(OrdinaryEvolutionOwnedPokemonCatalog.For(predecessor.Owned.Catalog), predecessor.Owned) ||
            predecessor.Catalog.Species.Count != 204 || predecessor.Owned.Catalog.Species.Count != 186 ||
            predecessor.Catalog.Moves.Count != 354 || !ReferenceEquals(predecessor.Catalog.Moves, predecessor.Owned.Catalog.Moves) ||
            AdditionalSpecies.Any(id => predecessor.Catalog.Species.ContainsKey(id) || predecessor.Owned.Catalog.Species.ContainsKey(id)) ||
            !AdditionalSpecies.Select(id => predecessor.Mechanics.Catalog.Species[id].SourceSpeciesId).SequenceEqual(new[] { 163, 164 }))
            throw new ArgumentException("memorial_pillar_opponent_predecessor");
        if (For(predecessor.Owned) is { } existing)
        {
            if (!ReferenceEquals(existing.Predecessor, predecessor)) throw new ArgumentException("memorial_pillar_opponent_rebind");
            return existing;
        }
        var proof = new MemorialPillarOpponentPokemonCatalog(predecessor);
        OwnedBattlePokemonCatalog.BindOpponentSuccessor(predecessor.Owned.SharedBattle, predecessor.Catalog,
            OrdinaryEvolutionOpponentPokemonCatalog.Profile, proof.Catalog, Profile);
        Proofs.Add(proof.Catalog, proof); Owners.Add(proof.Owned, proof); return proof;
    }
}
